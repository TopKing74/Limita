namespace Limita.Application.Features.Limits.Models;

/// <summary>
/// Detailed account limit configuration and real-time usage metrics.
/// </summary>
/// <param name="AccountId">Account GUID identifier.</param>
/// <param name="AccountNumber">Display account number.</param>
/// <param name="Currency">Account currency ISO code.</param>
/// <param name="PerTransactionLimit">Maximum amount allowed per single debit transaction.</param>
/// <param name="DailyLimit">Maximum cumulative debit allowance per calendar day (UTC).</param>
/// <param name="SpentToday">Total debit amount executed today so far.</param>
/// <param name="RemainingDailyLimit">Remaining available debit allowance for today.</param>
/// <param name="MonthlyLimit">Maximum cumulative debit allowance per calendar month (UTC).</param>
/// <param name="SpentThisMonth">Total debit amount executed in the current month so far.</param>
/// <param name="RemainingMonthlyLimit">Remaining available debit allowance for the current month.</param>
/// <param name="AsOfUtc">Timestamp of when limits and consumption were evaluated.</param>
public record AccountLimitsDto(
    Guid AccountId,
    string AccountNumber,
    string Currency,
    decimal PerTransactionLimit,
    decimal DailyLimit,
    decimal SpentToday,
    decimal RemainingDailyLimit,
    decimal MonthlyLimit,
    decimal SpentThisMonth,
    decimal RemainingMonthlyLimit,
    DateTime AsOfUtc);

/// <summary>
/// Request payload to update account-specific transaction and velocity limits.
/// </summary>
/// <param name="PerTransactionLimit">Optional new ceiling for single transactions.</param>
/// <param name="DailyLimit">Optional new cumulative daily ceiling.</param>
/// <param name="MonthlyLimit">Optional new cumulative monthly ceiling.</param>
public record UpdateAccountLimitsRequest(
    decimal? PerTransactionLimit,
    decimal? DailyLimit,
    decimal? MonthlyLimit);

/// <summary>
/// Internal result representing whether a financial operation satisfies policy and velocity limits.
/// </summary>
/// <param name="IsAllowed">True if transaction satisfies all limits; false otherwise.</param>
/// <param name="ErrorMessage">Explanation when limit check fails.</param>
/// <param name="RemainingDailyLimit">Available daily balance after or at time of evaluation.</param>
/// <param name="PerTransactionLimit">Effective per-transaction limit enforced.</param>
public record LimitCheckResult(
    bool IsAllowed,
    string? ErrorMessage = null,
    decimal? RemainingDailyLimit = null,
    decimal? PerTransactionLimit = null);
