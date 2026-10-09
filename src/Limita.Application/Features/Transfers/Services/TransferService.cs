using Microsoft.EntityFrameworkCore;
using Limita.Application.Common;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Transfers.Models;
using Limita.Domain.Entities;
using Limita.Domain.Enums;

namespace Limita.Application.Features.Transfers.Services;

/// <summary>
/// Service managing transfer validation, balance verification, and transfer lifecycle records.
/// </summary>
public class TransferService : ITransferService
{
    private readonly IApplicationDbContext _context;

    public TransferService(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<ApiResponse<TransferResponseDto>> CreateTransferAsync(
        string userId,
        CreateTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Retrieve sender's account and enforce server-side ownership
        var sourceAccount = await _context.Accounts
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == request.SourceAccountId, cancellationToken);

        if (sourceAccount == null || sourceAccount.UserId != userId)
        {
            return ApiResponse<TransferResponseDto>.Fail("Source account was not found or does not belong to you.");
        }

        // 2. Validate sender's account status
        if (sourceAccount.Status != AccountStatus.Active)
        {
            return ApiResponse<TransferResponseDto>.Fail($"Source account is {sourceAccount.Status.ToString().ToLower()} and cannot initiate transfers.");
        }

        // 3. Locate destination account by account number
        var trimmedDestAccountNumber = request.DestinationAccountNumber.Trim();
        var destinationAccount = await _context.Accounts
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.AccountNumber == trimmedDestAccountNumber, cancellationToken);

        if (destinationAccount == null)
        {
            return ApiResponse<TransferResponseDto>.Fail($"Destination account with number '{trimmedDestAccountNumber}' was not found.");
        }

        // 4. Prevent self-transfers to the exact same account
        if (sourceAccount.Id == destinationAccount.Id)
        {
            return ApiResponse<TransferResponseDto>.Fail("Cannot transfer funds to the same source account.");
        }

        // 5. Validate destination account status
        if (destinationAccount.Status != AccountStatus.Active)
        {
            return ApiResponse<TransferResponseDto>.Fail($"Destination account is {destinationAccount.Status.ToString().ToLower()} and cannot receive funds.");
        }

        // 6. Ensure matching currency (internal transfer restriction)
        if (!string.Equals(sourceAccount.Currency, destinationAccount.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<TransferResponseDto>.Fail($"Currency mismatch: Source account is in '{sourceAccount.Currency}' while destination account is in '{destinationAccount.Currency}'.");
        }

        // 7. Verify sufficient funds (Amount + Fee)
        const decimal fee = 0.00m; // Internal transfer zero fee policy
        var totalRequired = request.Amount + fee;
        if (sourceAccount.Balance < totalRequired)
        {
            return ApiResponse<TransferResponseDto>.Fail($"Insufficient balance. Your available balance is {sourceAccount.Balance:N2} {sourceAccount.Currency}, but total required is {totalRequired:N2} {sourceAccount.Currency}.");
        }

        // 8. Generate unique bank reference code
        var referenceCode = $"TRF-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        // 9. Create pending ledger transaction entry
        var pendingTransaction = new Transaction
        {
            AccountId = sourceAccount.Id,
            ReferenceNumber = referenceCode,
            Type = TransactionType.TransferOut,
            Amount = request.Amount,
            Currency = sourceAccount.Currency,
            Status = TransactionStatus.Pending,
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? $"Transfer to {destinationAccount.AccountNumber}"
                : request.Description.Trim()
        };

        _context.Transactions.Add(pendingTransaction);

        // 10. Create pending transfer record referencing the pending transaction
        var transfer = new Transfer
        {
            SourceAccountId = sourceAccount.Id,
            DestinationAccountId = destinationAccount.Id,
            TransactionId = pendingTransaction.Id,
            Transaction = pendingTransaction,
            Amount = request.Amount,
            Status = TransferStatus.Pending
        };

        _context.Transfers.Add(transfer);

        await _context.SaveChangesAsync(cancellationToken);

        var recipientName = destinationAccount.User != null && !string.IsNullOrWhiteSpace(destinationAccount.User.FullName)
            ? destinationAccount.User.FullName
            : "Limita Bank Customer";

        var responseDto = new TransferResponseDto(
            Id: transfer.Id,
            TransactionId: pendingTransaction.Id,
            ReferenceNumber: referenceCode,
            SourceAccountId: sourceAccount.Id,
            SourceAccountNumber: sourceAccount.AccountNumber,
            DestinationAccountId: destinationAccount.Id,
            DestinationAccountNumber: destinationAccount.AccountNumber,
            DestinationAccountHolderName: recipientName,
            Amount: request.Amount,
            Fee: fee,
            TotalDeduction: totalRequired,
            Currency: sourceAccount.Currency,
            Status: TransferStatus.Pending,
            StatusName: TransferStatus.Pending.ToString(),
            Description: pendingTransaction.Description,
            CreatedAt: transfer.CreatedAt,
            Message: "Transfer initiated successfully in Pending state. Please proceed to confirmation to complete execution.");

        return ApiResponse<TransferResponseDto>.Ok(responseDto, "Transfer request initiated successfully.");
    }

    /// <inheritdoc />
    public async Task<ApiResponse<ConfirmTransferResponseDto>> ConfirmTransferAsync(
        string userId,
        Guid transferId,
        CancellationToken cancellationToken = default)
    {
        // 1. Fetch transfer with accounts and pending transaction
        var transfer = await _context.Transfers
            .Include(t => t.SourceAccount)
                .ThenInclude(sa => sa!.User)
            .Include(t => t.DestinationAccount)
                .ThenInclude(da => da!.User)
            .Include(t => t.Transaction)
            .FirstOrDefaultAsync(t => t.Id == transferId, cancellationToken);

        if (transfer == null || transfer.SourceAccount == null || transfer.SourceAccount.UserId != userId)
        {
            return ApiResponse<ConfirmTransferResponseDto>.Fail("Transfer request was not found or does not belong to you.");
        }

        // 2. Validate lifecycle status
        if (transfer.Status == TransferStatus.Completed)
        {
            return ApiResponse<ConfirmTransferResponseDto>.Fail("This transfer has already been confirmed and completed.");
        }

        if (transfer.Status != TransferStatus.Pending)
        {
            return ApiResponse<ConfirmTransferResponseDto>.Fail($"Transfer cannot be confirmed because it is in '{transfer.Status}' state.");
        }

        if (transfer.DestinationAccount == null)
        {
            return ApiResponse<ConfirmTransferResponseDto>.Fail("Destination account for this transfer could not be found.");
        }

        // 3. Ensure accounts remain active
        if (transfer.SourceAccount.Status != AccountStatus.Active)
        {
            return ApiResponse<ConfirmTransferResponseDto>.Fail($"Source account is {transfer.SourceAccount.Status.ToString().ToLower()} and cannot perform transfers.");
        }

        if (transfer.DestinationAccount.Status != AccountStatus.Active)
        {
            return ApiResponse<ConfirmTransferResponseDto>.Fail($"Destination account is {transfer.DestinationAccount.Status.ToString().ToLower()} and cannot receive transfers.");
        }

        // 4. Re-check sender balance immediately before debiting
        const decimal fee = 0.00m;
        var totalRequired = transfer.Amount + fee;
        if (transfer.SourceAccount.Balance < totalRequired)
        {
            return ApiResponse<ConfirmTransferResponseDto>.Fail($"Insufficient balance to confirm transfer. Available balance is {transfer.SourceAccount.Balance:N2} {transfer.SourceAccount.Currency}, required is {totalRequired:N2} {transfer.SourceAccount.Currency}.");
        }

        // 5. ATOMIC FINANCIAL EXECUTION (wrapped with ExecutionStrategy for resilient retry compatibility)
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Debit sender
                transfer.SourceAccount.Balance -= totalRequired;

                // Credit recipient
                transfer.DestinationAccount.Balance += transfer.Amount;

                // Mark sender transaction as Completed
                if (transfer.Transaction != null)
                {
                    transfer.Transaction.Status = TransactionStatus.Completed;
                }

                // Create credit ledger entry for recipient (double-entry bookkeeping)
                var creditReference = $"{transfer.Transaction?.ReferenceNumber ?? transfer.Id.ToString("N")[..8]}-IN";
                var recipientTransaction = new Transaction
                {
                    AccountId = transfer.DestinationAccount.Id,
                    ReferenceNumber = creditReference,
                    Type = TransactionType.TransferIn,
                    Amount = transfer.Amount,
                    Currency = transfer.DestinationAccount.Currency,
                    Status = TransactionStatus.Completed,
                    Description = $"Transfer from {transfer.SourceAccount.AccountNumber}"
                };

                _context.Transactions.Add(recipientTransaction);

                // Mark transfer as Completed
                transfer.Status = TransferStatus.Completed;

                await _context.SaveChangesAsync(cancellationToken);
                await dbTransaction.CommitAsync(cancellationToken);

                var recipientName = transfer.DestinationAccount.User != null && !string.IsNullOrWhiteSpace(transfer.DestinationAccount.User.FullName)
                    ? transfer.DestinationAccount.User.FullName
                    : "Limita Bank Customer";

                var responseDto = new ConfirmTransferResponseDto(
                    TransferId: transfer.Id,
                    TransactionId: transfer.TransactionId,
                    ReferenceNumber: transfer.Transaction?.ReferenceNumber ?? creditReference,
                    SourceAccountNumber: transfer.SourceAccount.AccountNumber,
                    DestinationAccountNumber: transfer.DestinationAccount.AccountNumber,
                    RecipientName: recipientName,
                    TransferredAmount: transfer.Amount,
                    Fee: fee,
                    TotalDeducted: totalRequired,
                    RemainingBalance: transfer.SourceAccount.Balance,
                    Currency: transfer.SourceAccount.Currency,
                    Status: TransferStatus.Completed,
                    ExecutedAt: DateTime.UtcNow,
                    Message: "Transfer has been executed and completed successfully.");

                return ApiResponse<ConfirmTransferResponseDto>.Ok(responseDto, "Transfer confirmed and executed successfully.");
            }
            catch (DbUpdateConcurrencyException)
            {
                await dbTransaction.RollbackAsync(cancellationToken);
                return ApiResponse<ConfirmTransferResponseDto>.Fail("A concurrent transaction modified the account balance. Please try again.");
            }
            catch
            {
                await dbTransaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    /// <inheritdoc />
    public async Task<ApiResponse<CancelTransferResponseDto>> CancelTransferAsync(
        string userId,
        Guid transferId,
        CancellationToken cancellationToken = default)
    {
        // 1. Fetch transfer with source account and transaction
        var transfer = await _context.Transfers
            .Include(t => t.SourceAccount)
            .Include(t => t.Transaction)
            .FirstOrDefaultAsync(t => t.Id == transferId, cancellationToken);

        if (transfer == null || transfer.SourceAccount == null || transfer.SourceAccount.UserId != userId)
        {
            return ApiResponse<CancelTransferResponseDto>.Fail("Transfer request was not found or does not belong to you.");
        }

        // 2. Validate transfer lifecycle state
        if (transfer.Status == TransferStatus.Completed)
        {
            return ApiResponse<CancelTransferResponseDto>.Fail("Cannot cancel a completed transfer; funds have already settled.");
        }

        if (transfer.Status == TransferStatus.Cancelled)
        {
            return ApiResponse<CancelTransferResponseDto>.Fail("This transfer has already been cancelled.");
        }

        if (transfer.Status != TransferStatus.Pending)
        {
            return ApiResponse<CancelTransferResponseDto>.Fail($"Transfer cannot be cancelled because it is in '{transfer.Status}' state.");
        }

        // 3. Update status to Cancelled and update linked transaction to Failed
        transfer.Status = TransferStatus.Cancelled;

        if (transfer.Transaction != null)
        {
            transfer.Transaction.Status = TransactionStatus.Failed;
            transfer.Transaction.Description = string.IsNullOrWhiteSpace(transfer.Transaction.Description)
                ? "Transfer cancelled by user"
                : $"{transfer.Transaction.Description} (Cancelled by user)";
        }

        await _context.SaveChangesAsync(cancellationToken);

        var responseDto = new CancelTransferResponseDto(
            TransferId: transfer.Id,
            TransactionId: transfer.TransactionId,
            ReferenceNumber: transfer.Transaction?.ReferenceNumber ?? string.Empty,
            Status: TransferStatus.Cancelled,
            CancelledAt: DateTime.UtcNow,
            Message: "Pending transfer has been cancelled successfully.");

        return ApiResponse<CancelTransferResponseDto>.Ok(responseDto, "Transfer cancelled successfully.");
    }

    /// <inheritdoc />
    public async Task<ApiResponse<TransferResponseDto>> GetTransferByIdAsync(
        string userId,
        Guid transferId,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _context.Transfers
            .AsNoTracking()
            .Include(t => t.SourceAccount)
                .ThenInclude(sa => sa!.User)
            .Include(t => t.DestinationAccount)
                .ThenInclude(da => da!.User)
            .Include(t => t.Transaction)
            .FirstOrDefaultAsync(t => t.Id == transferId, cancellationToken);

        if (transfer == null)
        {
            return ApiResponse<TransferResponseDto>.Fail("Transfer was not found.");
        }

        var isSender = transfer.SourceAccount?.UserId == userId;
        var isRecipient = transfer.DestinationAccount?.UserId == userId;
        if (!isSender && !isRecipient)
        {
            return ApiResponse<TransferResponseDto>.Fail("You are not authorized to view this transfer.");
        }

        var recipientName = transfer.DestinationAccount?.User != null && !string.IsNullOrWhiteSpace(transfer.DestinationAccount.User.FullName)
            ? transfer.DestinationAccount.User.FullName
            : "Limita Bank Customer";

        var responseDto = new TransferResponseDto(
            Id: transfer.Id,
            TransactionId: transfer.TransactionId,
            ReferenceNumber: transfer.Transaction?.ReferenceNumber ?? string.Empty,
            SourceAccountId: transfer.SourceAccountId,
            SourceAccountNumber: transfer.SourceAccount?.AccountNumber ?? string.Empty,
            DestinationAccountId: transfer.DestinationAccountId,
            DestinationAccountNumber: transfer.DestinationAccount?.AccountNumber ?? string.Empty,
            DestinationAccountHolderName: recipientName,
            Amount: transfer.Amount,
            Fee: 0.00m,
            TotalDeduction: transfer.Amount,
            Currency: transfer.SourceAccount?.Currency ?? "EGP",
            Status: transfer.Status,
            StatusName: transfer.Status.ToString(),
            Description: transfer.Transaction?.Description,
            CreatedAt: transfer.CreatedAt,
            Message: $"Transfer status is {transfer.Status}.");

        return ApiResponse<TransferResponseDto>.Ok(responseDto);
    }

    /// <inheritdoc />
    public async Task<ApiResponse<PagedResult<TransferResponseDto>>> GetUserTransfersAsync(
        string userId,
        TransferListQuery query,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize < 1 ? 10 : (query.PageSize > 100 ? 100 : query.PageSize);

        // 1. Base query filtered directly by user ownership without extra database round-trips
        var baseQuery = _context.Transfers
            .AsNoTracking()
            .Where(t => (t.SourceAccount != null && t.SourceAccount.UserId == userId) ||
                        (t.DestinationAccount != null && t.DestinationAccount.UserId == userId));

        if (query.AccountId.HasValue)
        {
            var accountId = query.AccountId.Value;
            baseQuery = baseQuery.Where(t => t.SourceAccountId == accountId || t.DestinationAccountId == accountId);
        }

        if (query.Status.HasValue)
        {
            baseQuery = baseQuery.Where(t => t.Status == query.Status.Value);
        }

        // 2. Perform lean COUNT query prior to joining navigation entities
        var totalCount = await baseQuery.CountAsync(cancellationToken);

        if (totalCount == 0)
        {
            var emptyResult = new PagedResult<TransferResponseDto>(Array.Empty<TransferResponseDto>(), 0, pageNumber, pageSize);
            return ApiResponse<PagedResult<TransferResponseDto>>.Ok(emptyResult);
        }

        // 3. Apply navigation joins only on the requested page slice
        var transfers = await baseQuery
            .Include(t => t.SourceAccount)
                .ThenInclude(sa => sa!.User)
            .Include(t => t.DestinationAccount)
                .ThenInclude(da => da!.User)
            .Include(t => t.Transaction)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = transfers.Select(t =>
        {
            var recipientName = t.DestinationAccount?.User != null && !string.IsNullOrWhiteSpace(t.DestinationAccount.User.FullName)
                ? t.DestinationAccount.User.FullName
                : "Limita Bank Customer";

            return new TransferResponseDto(
                Id: t.Id,
                TransactionId: t.TransactionId,
                ReferenceNumber: t.Transaction?.ReferenceNumber ?? string.Empty,
                SourceAccountId: t.SourceAccountId,
                SourceAccountNumber: t.SourceAccount?.AccountNumber ?? string.Empty,
                DestinationAccountId: t.DestinationAccountId,
                DestinationAccountNumber: t.DestinationAccount?.AccountNumber ?? string.Empty,
                DestinationAccountHolderName: recipientName,
                Amount: t.Amount,
                Fee: 0.00m,
                TotalDeduction: t.Amount,
                Currency: t.SourceAccount?.Currency ?? "EGP",
                Status: t.Status,
                StatusName: t.Status.ToString(),
                Description: t.Transaction?.Description,
                CreatedAt: t.CreatedAt,
                Message: $"Transfer status is {t.Status}.");
        }).ToList();

        var pagedResult = new PagedResult<TransferResponseDto>(items, totalCount, pageNumber, pageSize);
        return ApiResponse<PagedResult<TransferResponseDto>>.Ok(pagedResult);
    }
}

