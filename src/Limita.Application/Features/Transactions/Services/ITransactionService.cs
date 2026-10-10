using Limita.Application.Common;
using Limita.Application.Features.Transactions.Models;

namespace Limita.Application.Features.Transactions.Services;

/// <summary>
/// Service contract defining operations for querying account transactions, receipts, and summaries.
/// </summary>
public interface ITransactionService
{
    /// <summary>
    /// Retrieves a paginated list of transactions filtered by criteria, strictly scoped to the authenticated user.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="filter">Filtering and pagination parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated result containing matching transactions.</returns>
    Task<PagedResult<TransactionDto>> GetTransactionsAsync(
        string userId,
        TransactionFilterRequest filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves detailed receipt data for a single transaction by ID, validating user ownership.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="transactionId">The unique transaction GUID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transaction details if found and owned by the user, otherwise null.</returns>
    Task<TransactionDetailsDto?> GetTransactionByIdAsync(
        string userId,
        Guid transactionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes aggregated cash inflow, outflow, and categorized breakdown for the user's accounts.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="accountId">Optional account filter. If null, aggregates across all accounts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Financial summary and spending breakdown.</returns>
    Task<TransactionSummaryDto> GetTransactionsSummaryAsync(
        string userId,
        Guid? accountId,
        CancellationToken cancellationToken = default);
}
