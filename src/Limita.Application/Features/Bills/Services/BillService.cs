using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Bills.Models;
using Limita.Application.Features.Notifications.Services;
using Limita.Domain.Entities;
using Limita.Domain.Enums;

namespace Limita.Application.Features.Bills.Services;

public class BillService : IBillService
{
    private readonly IApplicationDbContext _context;
    private readonly INotificationService? _notificationService;
    private readonly ILogger<BillService>? _logger;

    public BillService(
        IApplicationDbContext context,
        INotificationService? notificationService = null,
        ILogger<BillService>? logger = null)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<BillDto> CreateBillAsync(string userId, CreateBillRequest request, CancellationToken cancellationToken = default)
    {
        // Verify provider exists and is active
        var provider = await _context.BillProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProviderId, cancellationToken);

        if (provider == null)
            throw new InvalidOperationException("The specified Bill Provider does not exist.");

        if (!provider.IsActive)
            throw new InvalidOperationException("The specified Bill Provider is currently inactive.");

        var bill = new Bill
        {
            UserId = userId,
            ProviderId = request.ProviderId,
            BillingNumber = request.BillingNumber,
            Amount = request.Amount,
            DueDate = request.DueDate,
            Status = BillStatus.Pending
        };

        _context.Bills.Add(bill);
        await _context.SaveChangesAsync(cancellationToken);

        return new BillDto(
            bill.Id,
            bill.ProviderId,
            provider.Name,
            bill.BillingNumber,
            bill.Amount,
            bill.DueDate,
            bill.Status,
            bill.CreatedAt
        );
    }

    public async Task<IEnumerable<BillDto>> GetUserBillsAsync(string userId, Guid? providerId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Bills
            .AsNoTracking()
            .Include(b => b.Provider)
            .Where(b => b.UserId == userId);

        if (providerId.HasValue)
        {
            query = query.Where(b => b.ProviderId == providerId.Value);
        }

        var bills = await query.OrderByDescending(b => b.CreatedAt).ToListAsync(cancellationToken);

        return bills.Select(b => new BillDto(
            b.Id,
            b.ProviderId,
            b.Provider?.Name ?? string.Empty,
            b.BillingNumber,
            b.Amount,
            b.DueDate,
            b.Status,
            b.CreatedAt
        ));
    }

    public async Task<BillPaymentResponseDto> PayBillAsync(string userId, Guid billId, PayBillRequest request, CancellationToken cancellationToken = default)
    {
        var bill = await _context.Bills
            .FirstOrDefaultAsync(b => b.Id == billId && b.UserId == userId, cancellationToken);

        if (bill == null)
            throw new KeyNotFoundException("Bill not found or you do not have access to it.");

        if (bill.Status == BillStatus.Paid)
            throw new InvalidOperationException("The bill is already paid.");

        if (bill.Status != BillStatus.Pending)
            throw new InvalidOperationException($"The bill cannot be paid because its status is {bill.Status}.");

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == request.AccountId && a.UserId == userId, cancellationToken);

        if (account == null)
            throw new KeyNotFoundException("Account not found or you do not have access to it.");

        if (account.Status != AccountStatus.Active)
            throw new InvalidOperationException("Cannot pay from an inactive or blocked account.");

        if (account.Balance < bill.Amount)
            throw new InvalidOperationException("Insufficient available balance.");

        async Task<BillPaymentResponseDto> ExecutePayBillCoreAsync()
        {
            // Debit the account
            account.Balance -= bill.Amount;
            account.UpdatedAt = DateTime.UtcNow;

            // Transition bill status atomically
            bill.Status = BillStatus.Paid;
            bill.UpdatedAt = DateTime.UtcNow;

            // Create ledger transaction
            var transaction = new Transaction
            {
                AccountId = account.Id,
                ReferenceNumber = $"BPY-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
                Type = TransactionType.BillPayment,
                Amount = bill.Amount,
                Currency = account.Currency,
                Status = TransactionStatus.Completed,
                Description = $"Payment for bill {bill.BillingNumber}",
                CreatedAt = DateTime.UtcNow
            };

            await _context.Transactions.AddAsync(transaction, cancellationToken);

            try
            {
                // Atomically persist changes
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException("The account balance or bill status was modified by another transaction. Please try again.");
            }

            return new BillPaymentResponseDto(
                bill.Id,
                transaction.Id,
                transaction.ReferenceNumber,
                bill.Amount,
                DateTime.UtcNow
            );
        }

        BillPaymentResponseDto result;
        if (_context.Database == null)
        {
            result = await ExecutePayBillCoreAsync();
        }
        else
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            result = await strategy.ExecuteAsync(async () =>
            {
                await using var dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var res = await ExecutePayBillCoreAsync();
                    await dbTransaction.CommitAsync(cancellationToken);
                    return res;
                }
                catch
                {
                    await dbTransaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });
        }

        if (_notificationService != null)
        {
            try
            {
                await _notificationService.CreateAndSendNotificationAsync(
                    userId,
                    "Bill Paid",
                    $"Payment of {bill.Amount:N2} {account.Currency} for bill {bill.BillingNumber} was completed successfully.",
                    "Transaction",
                    result.ReferenceNumber,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to send notification for bill payment {BillId}", bill.Id);
            }
        }

        return result;
    }
}
