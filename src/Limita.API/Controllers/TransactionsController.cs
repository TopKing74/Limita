using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Limita.Application.Common;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Transactions.Models;
using Limita.Application.Features.Transactions.Services;

namespace Limita.API.Controllers;

/// <summary>
/// Endpoints for inspecting transaction history, account statements, receipts, and categorized spending summaries.
/// </summary>
[Authorize]
public class TransactionsController : BaseApiController
{
    private readonly ITransactionService _transactionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<TransactionFilterRequest> _filterValidator;

    public TransactionsController(
        ITransactionService transactionService,
        ICurrentUserService currentUserService,
        IValidator<TransactionFilterRequest> filterValidator)
    {
        _transactionService = transactionService;
        _currentUserService = currentUserService;
        _filterValidator = filterValidator;
    }

    /// <summary>
    /// Retrieve a paginated transaction history statement with optional filters.
    /// </summary>
    /// <param name="filter">Filtering parameters (accountId, type, status, date range, page, pageSize).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of matching transaction records.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TransactionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<PagedResult<TransactionDto>>>> GetTransactions(
        [FromQuery] TransactionFilterRequest filter,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var validationResult = await _filterValidator.ValidateAsync(filter, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var result = await _transactionService.GetTransactionsAsync(userId, filter, cancellationToken);
        return Ok(ApiResponse<PagedResult<TransactionDto>>.Ok(result, "Transactions retrieved successfully."));
    }

    /// <summary>
    /// Retrieve full receipt details of a specific transaction by its ID.
    /// </summary>
    /// <param name="id">The unique GUID of the transaction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transaction details and financial metadata.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TransactionDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TransactionDetailsDto>>> GetTransactionById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var transaction = await _transactionService.GetTransactionByIdAsync(userId, id, cancellationToken);
        if (transaction is null)
        {
            return NotFound(ApiResponse.Fail("Transaction not found or you do not have permission to view it."));
        }

        return Ok(ApiResponse<TransactionDetailsDto>.Ok(transaction, "Transaction details retrieved successfully."));
    }

    /// <summary>
    /// Retrieve aggregated cash flow totals (inflow, outflow, net) and category spending breakdown.
    /// </summary>
    /// <param name="accountId">Optional account filter. If null, summarizes across all user accounts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Financial summary and categorized volume breakdown.</returns>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<TransactionSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<TransactionSummaryDto>>> GetTransactionsSummary(
        [FromQuery] Guid? accountId,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var summary = await _transactionService.GetTransactionsSummaryAsync(userId, accountId, cancellationToken);
        return Ok(ApiResponse<TransactionSummaryDto>.Ok(summary, "Transaction summary retrieved successfully."));
    }

    private string? GetRequiredUserId() =>
        string.IsNullOrWhiteSpace(_currentUserService.UserId) ? null : _currentUserService.UserId;
}
