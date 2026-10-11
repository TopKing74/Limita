using Limita.Application.Common;
using Limita.Application.Features.Limits.Models;

namespace Limita.Application.Features.Limits.Services;

/// <summary>
/// Service contract managing per-transaction ceilings, daily/monthly velocity checks, and limits configuration.
/// </summary>
public interface ILimitService
{
    /// <summary>
    /// Validates whether a proposed debit transaction satisfies per-transaction and cumulative daily/monthly limits.
    /// </summary>
    /// <param name="accountId">Account GUID to validate against.</param>
    /// <param name="amount">Transaction amount.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation verdict containing allowed status and actionable feedback.</returns>
    Task<LimitCheckResult> ValidateDebitLimitAsync(
        Guid accountId,
        decimal amount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the current limit rules and real-time consumption metrics for an owned account.
    /// </summary>
    /// <param name="userId">Authenticated user ID.</param>
    /// <param name="accountId">Target account ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed limits breakdown DTO.</returns>
    Task<ApiResponse<AccountLimitsDto>> GetAccountLimitsAsync(
        string userId,
        Guid accountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates custom per-transaction, daily, or monthly limits for an owned account.
    /// </summary>
    /// <param name="userId">Authenticated user ID.</param>
    /// <param name="accountId">Target account ID.</param>
    /// <param name="request">Limit changes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated limits record.</returns>
    Task<ApiResponse<AccountLimitsDto>> UpdateAccountLimitsAsync(
        string userId,
        Guid accountId,
        UpdateAccountLimitsRequest request,
        CancellationToken cancellationToken = default);
}
