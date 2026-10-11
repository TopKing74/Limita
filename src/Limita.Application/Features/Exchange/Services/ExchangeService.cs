using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Limita.Application.Common;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Exchange.Models;
using Limita.Application.Features.Limits.Services;
using Limita.Domain.Entities;
using Limita.Domain.Enums;

namespace Limita.Application.Features.Exchange.Services;

public class ExchangeService : IExchangeService
{
    private readonly IApplicationDbContext _context;
    private readonly ILimitService _limitService;

    public ExchangeService(
        IApplicationDbContext context,
        ILimitService? limitService = null)
    {
        _context = context;
        _limitService = limitService ?? new LimitService(context);
    }

    public async Task<ApiResponse<ExchangeQuoteDto>> GetQuoteAsync(
        string userId,
        ExchangeQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var accountsResult = await LoadOwnedAccountsAsync(
            userId, request.SourceAccountId, request.DestinationAccountId, cancellationToken);

        if (!accountsResult.Success)
            return ApiResponse<ExchangeQuoteDto>.Fail(accountsResult.Message, accountsResult.Errors);

        var (source, destination) = accountsResult.Data!;

        if (string.Equals(source.Currency, destination.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<ExchangeQuoteDto>.Fail(
                "Source and destination accounts must have different currencies for an exchange.");
        }

        var rate = await FindRateAsync(source.Currency, destination.Currency, cancellationToken);
        if (rate is null)
        {
            return ApiResponse<ExchangeQuoteDto>.Fail(
                $"No exchange rate is configured for {source.Currency} → {destination.Currency}.");
        }

        var convertedAmount = RoundMoney(request.Amount * rate.Rate);

        var dto = new ExchangeQuoteDto(
            SourceAccountId: source.Id,
            SourceAccountNumber: source.AccountNumber,
            FromCurrency: source.Currency,
            DestinationAccountId: destination.Id,
            DestinationAccountNumber: destination.AccountNumber,
            ToCurrency: destination.Currency,
            SourceAmount: request.Amount,
            Rate: rate.Rate,
            ConvertedAmount: convertedAmount,
            RateUpdatedAt: rate.UpdatedAt,
            QuotedAt: DateTime.UtcNow,
            Message: $"Quote: {request.Amount:N2} {source.Currency} = {convertedAmount:N2} {destination.Currency} at rate {rate.Rate}.");

        return ApiResponse<ExchangeQuoteDto>.Ok(dto, "Exchange quote calculated successfully.");
    }

    public async Task<ApiResponse<ExchangeResultDto>> ExecuteExchangeAsync(
        string userId,
        ExecuteExchangeRequest request,
        CancellationToken cancellationToken = default)
    {
        var accountsResult = await LoadOwnedAccountsAsync(
            userId, request.SourceAccountId, request.DestinationAccountId, cancellationToken);

        if (!accountsResult.Success)
            return ApiResponse<ExchangeResultDto>.Fail(accountsResult.Message, accountsResult.Errors);

        var (source, destination) = accountsResult.Data!;

        if (source.Status != AccountStatus.Active)
        {
            return ApiResponse<ExchangeResultDto>.Fail(
                $"Source account is {source.Status.ToString().ToLower()} and cannot perform exchanges.");
        }

        if (destination.Status != AccountStatus.Active)
        {
            return ApiResponse<ExchangeResultDto>.Fail(
                $"Destination account is {destination.Status.ToString().ToLower()} and cannot receive exchanges.");
        }

        if (string.Equals(source.Currency, destination.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<ExchangeResultDto>.Fail(
                "Source and destination accounts must have different currencies for an exchange.");
        }

        var rate = await FindRateAsync(source.Currency, destination.Currency, cancellationToken);
        if (rate is null)
        {
            return ApiResponse<ExchangeResultDto>.Fail(
                $"No exchange rate is configured for {source.Currency} → {destination.Currency}.");
        }

        if (source.Balance < request.Amount)
        {
            return ApiResponse<ExchangeResultDto>.Fail(
                "Insufficient balance");
        }

        var convertedAmount = RoundMoney(request.Amount * rate.Rate);
        if (convertedAmount <= 0)
        {
            return ApiResponse<ExchangeResultDto>.Fail(
                "Converted amount rounds to zero. Increase the source amount.");
        }

        // Validate debit limit prior to starting financial mutation
        var limitCheck = await _limitService.ValidateDebitLimitAsync(source.Id, request.Amount, cancellationToken);
        if (!limitCheck.IsAllowed)
        {
            return ApiResponse<ExchangeResultDto>.Fail(
                limitCheck.ErrorMessage ?? "Currency exchange debit limit exceeded.");
        }

        var appliedRate = rate.Rate;
        var referenceBase = $"FX-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var debitReference = $"{referenceBase}-OUT";
        var creditReference = $"{referenceBase}-IN";

        async Task<ApiResponse<ExchangeResultDto>> ExecuteExchangeCoreAsync()
        {
            _context.ChangeTracker?.Clear();

            // Idempotent execution check: Verify if the exchange transaction was already committed
            var existingDebitTx = await _context.Transactions
                .FirstOrDefaultAsync(t => t.ReferenceNumber == debitReference, cancellationToken);

            if (existingDebitTx != null)
            {
                var existingCreditTx = await _context.Transactions
                    .FirstOrDefaultAsync(t => t.ReferenceNumber == creditReference, cancellationToken);

                var existingSource = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Id == request.SourceAccountId, cancellationToken);
                var existingDestination = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Id == request.DestinationAccountId, cancellationToken);

                if (existingCreditTx != null && existingSource != null && existingDestination != null)
                {
                    var existingResult = new ExchangeResultDto(
                        ReferenceNumber: referenceBase,
                        SourceAccountId: existingSource.Id,
                        SourceAccountNumber: existingSource.AccountNumber,
                        FromCurrency: existingSource.Currency,
                        DebitedAmount: request.Amount,
                        SourceRemainingBalance: existingSource.Balance,
                        DestinationAccountId: existingDestination.Id,
                        DestinationAccountNumber: existingDestination.AccountNumber,
                        ToCurrency: existingDestination.Currency,
                        CreditedAmount: convertedAmount,
                        DestinationNewBalance: existingDestination.Balance,
                        AppliedRate: appliedRate,
                        DebitTransactionId: existingDebitTx.Id,
                        CreditTransactionId: existingCreditTx.Id,
                        ExecutedAt: existingDebitTx.CreatedAt,
                        Message: $"Exchanged {request.Amount:N2} {existingSource.Currency} to {convertedAmount:N2} {existingDestination.Currency}.");

                    return ApiResponse<ExchangeResultDto>.Ok(existingResult, "Currency exchange executed successfully.");
                }
            }

            IDbContextTransaction? dbTransaction = null;
            if (_context.Database != null)
            {
                dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            }
            else
            {
                dbTransaction = await _context.BeginTransactionAsync(cancellationToken);
            }

            try
            {
                // Re-load fresh entities inside the transaction for concurrency safety and clean state
                var sourceAccount = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Id == request.SourceAccountId, cancellationToken);
                var destinationAccount = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Id == request.DestinationAccountId, cancellationToken);

                if (sourceAccount is null || destinationAccount is null)
                {
                    if (dbTransaction != null)
                    {
                        await dbTransaction.RollbackAsync(cancellationToken);
                    }
                    return ApiResponse<ExchangeResultDto>.Fail("One or both accounts could not be found.");
                }

                if (sourceAccount.Status != AccountStatus.Active)
                {
                    if (dbTransaction != null)
                    {
                        await dbTransaction.RollbackAsync(cancellationToken);
                    }
                    return ApiResponse<ExchangeResultDto>.Fail(
                        $"Source account is {sourceAccount.Status.ToString().ToLower()} and cannot perform exchanges.");
                }

                if (destinationAccount.Status != AccountStatus.Active)
                {
                    if (dbTransaction != null)
                    {
                        await dbTransaction.RollbackAsync(cancellationToken);
                    }
                    return ApiResponse<ExchangeResultDto>.Fail(
                        $"Destination account is {destinationAccount.Status.ToString().ToLower()} and cannot receive exchanges.");
                }

                if (sourceAccount.Balance < request.Amount)
                {
                    if (dbTransaction != null)
                    {
                        await dbTransaction.RollbackAsync(cancellationToken);
                    }
                    return ApiResponse<ExchangeResultDto>.Fail("Insufficient balance");
                }

                var freshLimitCheck = await _limitService.ValidateDebitLimitAsync(sourceAccount.Id, request.Amount, cancellationToken);
                if (!freshLimitCheck.IsAllowed)
                {
                    if (dbTransaction != null)
                    {
                        await dbTransaction.RollbackAsync(cancellationToken);
                    }
                    return ApiResponse<ExchangeResultDto>.Fail(
                        freshLimitCheck.ErrorMessage ?? "Currency exchange debit limit exceeded.");
                }

                sourceAccount.Balance -= request.Amount;
                destinationAccount.Balance += convertedAmount;

                var debitTx = new Transaction
                {
                    AccountId = sourceAccount.Id,
                    ReferenceNumber = debitReference,
                    Type = TransactionType.ExchangeOut,
                    Amount = request.Amount,
                    Currency = sourceAccount.Currency,
                    Status = TransactionStatus.Completed,
                    Description = $"Currency exchange to {destinationAccount.AccountNumber} ({destinationAccount.Currency}) @ {appliedRate}"
                };

                var creditTx = new Transaction
                {
                    AccountId = destinationAccount.Id,
                    ReferenceNumber = creditReference,
                    Type = TransactionType.ExchangeIn,
                    Amount = convertedAmount,
                    Currency = destinationAccount.Currency,
                    Status = TransactionStatus.Completed,
                    Description = $"Currency exchange from {sourceAccount.AccountNumber} ({sourceAccount.Currency}) @ {appliedRate}"
                };

                _context.Transactions.Add(debitTx);
                _context.Transactions.Add(creditTx);

                await _context.SaveChangesAsync(cancellationToken);

                if (dbTransaction != null)
                {
                    await dbTransaction.CommitAsync(cancellationToken);
                }

                var result = new ExchangeResultDto(
                    ReferenceNumber: referenceBase,
                    SourceAccountId: sourceAccount.Id,
                    SourceAccountNumber: sourceAccount.AccountNumber,
                    FromCurrency: sourceAccount.Currency,
                    DebitedAmount: request.Amount,
                    SourceRemainingBalance: sourceAccount.Balance,
                    DestinationAccountId: destinationAccount.Id,
                    DestinationAccountNumber: destinationAccount.AccountNumber,
                    ToCurrency: destinationAccount.Currency,
                    CreditedAmount: convertedAmount,
                    DestinationNewBalance: destinationAccount.Balance,
                    AppliedRate: appliedRate,
                    DebitTransactionId: debitTx.Id,
                    CreditTransactionId: creditTx.Id,
                    ExecutedAt: DateTime.UtcNow,
                    Message: $"Exchanged {request.Amount:N2} {sourceAccount.Currency} to {convertedAmount:N2} {destinationAccount.Currency}.");

                return ApiResponse<ExchangeResultDto>.Ok(result, "Currency exchange executed successfully.");
            }
            catch (DbUpdateConcurrencyException)
            {
                if (dbTransaction != null)
                {
                    await dbTransaction.RollbackAsync(cancellationToken);
                }
                _context.ChangeTracker?.Clear();
                return ApiResponse<ExchangeResultDto>.Fail(
                    "A concurrent transaction modified an account balance. Please try again.");
            }
            catch
            {
                if (dbTransaction != null)
                {
                    await dbTransaction.RollbackAsync(cancellationToken);
                }
                _context.ChangeTracker?.Clear();
                throw;
            }
            finally
            {
                if (dbTransaction != null)
                {
                    await dbTransaction.DisposeAsync();
                }
            }
        }

        if (_context.Database != null)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () => await ExecuteExchangeCoreAsync());
        }
        else
        {
            return await ExecuteExchangeCoreAsync();
        }
    }

    private async Task<ApiResponse<(Account Source, Account Destination)>> LoadOwnedAccountsAsync(
        string userId,
        Guid sourceAccountId,
        Guid destinationAccountId,
        CancellationToken cancellationToken)
    {
        var source = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == sourceAccountId, cancellationToken);

        if (source is null || source.UserId != userId)
        {
            return ApiResponse<(Account, Account)>.Fail(
                "Source account was not found or does not belong to you.");
        }

        var destination = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == destinationAccountId, cancellationToken);

        if (destination is null || destination.UserId != userId)
        {
            return ApiResponse<(Account, Account)>.Fail(
                "Destination account was not found or does not belong to you.");
        }

        return ApiResponse<(Account, Account)>.Ok((source, destination));
    }

    private async Task<ExchangeRate?> FindRateAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken)
    {
        var from = fromCurrency.Trim().ToUpperInvariant();
        var to = toCurrency.Trim().ToUpperInvariant();

        return await _context.ExchangeRates
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.FromCurrency == from && r.ToCurrency == to,
                cancellationToken);
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
