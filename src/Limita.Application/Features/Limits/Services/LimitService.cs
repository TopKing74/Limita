using Microsoft.EntityFrameworkCore;
using Limita.Application.Common;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Limits.Models;
using Limita.Domain.Enums;

namespace Limita.Application.Features.Limits.Services;

/// <summary>
/// Core implementation of banking velocity and transaction limit enforcement.
/// </summary>
public class LimitService : ILimitService
{
    private static readonly TransactionType[] DebitTypes =
    [
        TransactionType.TransferOut,
        TransactionType.Withdrawal,
        TransactionType.BillPayment,
        TransactionType.MobileRecharge,
        TransactionType.ExchangeOut
    ];

    private readonly IApplicationDbContext _context;

    public LimitService(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<LimitCheckResult> ValidateDebitLimitAsync(
        Guid accountId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            return new LimitCheckResult(
                IsAllowed: false,
                ErrorMessage: "Transaction amount must be strictly greater than zero.");
        }

        if (_context.Accounts is null)
        {
            return new LimitCheckResult(IsAllowed: true);
        }

        var account = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

        if (account is null)
        {
            return new LimitCheckResult(
                IsAllowed: false,
                ErrorMessage: "Account not found.");
        }

        var effectivePerTxLimit = account.PerTransactionLimit > 0
            ? account.PerTransactionLimit
            : account.DailyLimit;

        // 1. Enforce Per-Transaction Limit
        if (amount > effectivePerTxLimit)
        {
            return new LimitCheckResult(
                IsAllowed: false,
                ErrorMessage: $"Transaction amount ({amount:N2} {account.Currency}) exceeds the maximum per-transaction limit of {effectivePerTxLimit:N2} {account.Currency}.",
                RemainingDailyLimit: null,
                PerTransactionLimit: effectivePerTxLimit);
        }

        // 2. Calculate Daily Spent (UTC start of day)
        var todayStartUtc = DateTime.UtcNow.Date;
        var spentToday = _context.Transactions is null
            ? 0m
            : await _context.Transactions
                .AsNoTracking()
                .Where(t => t.AccountId == accountId
                         && DebitTypes.Contains(t.Type)
                         && t.Status == TransactionStatus.Completed
                         && t.CreatedAt >= todayStartUtc)
                .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

        var remainingDaily = Math.Max(0m, account.DailyLimit - spentToday);

        // 3. Enforce Daily Cumulative Limit
        if (spentToday + amount > account.DailyLimit)
        {
            return new LimitCheckResult(
                IsAllowed: false,
                ErrorMessage: $"Daily transfer limit exceeded. You have spent {spentToday:N2} {account.Currency} today out of your {account.DailyLimit:N2} {account.Currency} daily limit. Remaining available limit for today is {remainingDaily:N2} {account.Currency}.",
                RemainingDailyLimit: remainingDaily,
                PerTransactionLimit: effectivePerTxLimit);
        }

        // 4. Calculate Monthly Spent (UTC start of current month)
        var now = DateTime.UtcNow;
        var monthStartUtc = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var spentThisMonth = _context.Transactions is null
            ? 0m
            : await _context.Transactions
                .AsNoTracking()
                .Where(t => t.AccountId == accountId
                         && DebitTypes.Contains(t.Type)
                         && t.Status == TransactionStatus.Completed
                         && t.CreatedAt >= monthStartUtc)
                .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

        var remainingMonthly = Math.Max(0m, account.MonthlyLimit - spentThisMonth);

        // 5. Enforce Monthly Cumulative Limit
        if (spentThisMonth + amount > account.MonthlyLimit)
        {
            return new LimitCheckResult(
                IsAllowed: false,
                ErrorMessage: $"Monthly transaction limit exceeded. You have spent {spentThisMonth:N2} {account.Currency} this month out of your {account.MonthlyLimit:N2} {account.Currency} monthly limit. Remaining available limit for this month is {remainingMonthly:N2} {account.Currency}.",
                RemainingDailyLimit: remainingDaily,
                PerTransactionLimit: effectivePerTxLimit);
        }

        return new LimitCheckResult(
            IsAllowed: true,
            RemainingDailyLimit: remainingDaily - amount,
            PerTransactionLimit: effectivePerTxLimit);
    }

    /// <inheritdoc />
    public async Task<ApiResponse<AccountLimitsDto>> GetAccountLimitsAsync(
        string userId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var account = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId, cancellationToken);

        if (account is null)
        {
            return ApiResponse<AccountLimitsDto>.Fail("Account not found or access denied.");
        }

        var todayStartUtc = DateTime.UtcNow.Date;
        var spentToday = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId
                     && DebitTypes.Contains(t.Type)
                     && t.Status == TransactionStatus.Completed
                     && t.CreatedAt >= todayStartUtc)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

        var now = DateTime.UtcNow;
        var monthStartUtc = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var spentThisMonth = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId
                     && DebitTypes.Contains(t.Type)
                     && t.Status == TransactionStatus.Completed
                     && t.CreatedAt >= monthStartUtc)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

        var effectivePerTx = account.PerTransactionLimit > 0
            ? account.PerTransactionLimit
            : account.DailyLimit;

        var dto = new AccountLimitsDto(
            AccountId: account.Id,
            AccountNumber: account.AccountNumber,
            Currency: account.Currency,
            PerTransactionLimit: effectivePerTx,
            DailyLimit: account.DailyLimit,
            SpentToday: spentToday,
            RemainingDailyLimit: Math.Max(0m, account.DailyLimit - spentToday),
            MonthlyLimit: account.MonthlyLimit,
            SpentThisMonth: spentThisMonth,
            RemainingMonthlyLimit: Math.Max(0m, account.MonthlyLimit - spentThisMonth),
            AsOfUtc: DateTime.UtcNow);

        return ApiResponse<AccountLimitsDto>.Ok(dto, "Account limits retrieved successfully.");
    }

    /// <inheritdoc />
    public async Task<ApiResponse<AccountLimitsDto>> UpdateAccountLimitsAsync(
        string userId,
        Guid accountId,
        UpdateAccountLimitsRequest request,
        CancellationToken cancellationToken = default)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId, cancellationToken);

        if (account is null)
        {
            return ApiResponse<AccountLimitsDto>.Fail("Account not found or access denied.");
        }

        if (account.Status != AccountStatus.Active)
        {
            return ApiResponse<AccountLimitsDto>.Fail("Cannot update limits on an inactive account.");
        }

        // Compute merged final values (Incoming Values merged with Existing Stored Values)
        var mergedPerTx = request.PerTransactionLimit ?? account.PerTransactionLimit;
        var mergedDaily = request.DailyLimit ?? account.DailyLimit;
        var mergedMonthly = request.MonthlyLimit ?? account.MonthlyLimit;

        // Enforce business rule: PerTransactionLimit <= DailyLimit <= MonthlyLimit on the final merged state
        if (mergedPerTx > mergedDaily)
        {
            return ApiResponse<AccountLimitsDto>.Fail(
                $"Per-transaction limit ({mergedPerTx:N2} {account.Currency}) cannot exceed daily limit ({mergedDaily:N2} {account.Currency}).");
        }

        if (mergedDaily > mergedMonthly)
        {
            return ApiResponse<AccountLimitsDto>.Fail(
                $"Daily limit ({mergedDaily:N2} {account.Currency}) cannot exceed monthly limit ({mergedMonthly:N2} {account.Currency}).");
        }

        if (mergedPerTx > mergedMonthly)
        {
            return ApiResponse<AccountLimitsDto>.Fail(
                $"Per-transaction limit ({mergedPerTx:N2} {account.Currency}) cannot exceed monthly limit ({mergedMonthly:N2} {account.Currency}).");
        }

        account.PerTransactionLimit = mergedPerTx;
        account.DailyLimit = mergedDaily;
        account.MonthlyLimit = mergedMonthly;

        await _context.SaveChangesAsync(cancellationToken);

        return await GetAccountLimitsAsync(userId, accountId, cancellationToken);
    }
}
