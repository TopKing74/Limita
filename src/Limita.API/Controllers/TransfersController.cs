using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Limita.Application.Common;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Transfers.Models;
using Limita.Application.Features.Transfers.Services;

namespace Limita.API.Controllers;

/// <summary>
/// Endpoints for initiating, executing, and tracking bank transfers.
/// </summary>
[Authorize]
public class TransfersController : BaseApiController
{
    private readonly ITransferService _transferService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<CreateTransferRequest> _validator;

    public TransfersController(
        ITransferService transferService,
        ICurrentUserService currentUserService,
        IValidator<CreateTransferRequest> validator)
    {
        _transferService = transferService;
        _currentUserService = currentUserService;
        _validator = validator;
    }

    /// <summary>
    /// Initiate a new bank transfer request in Pending state awaiting confirmation.
    /// </summary>
    /// <param name="request">Transfer details including source account, destination account number, and amount.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created transfer details with pending status and recipient confirmation info.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TransferResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<TransferResponseDto>>> CreateTransfer(
        [FromBody] CreateTransferRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var result = await _transferService.CreateTransferAsync(userId, request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Confirm and execute a pending transfer atomically.
    /// Debits the sender, credits the recipient, and records transactions in the ledger.
    /// </summary>
    /// <param name="id">The GUID of the pending transfer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Execution receipt with updated balances and status.</returns>
    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType(typeof(ApiResponse<ConfirmTransferResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<ConfirmTransferResponseDto>>> ConfirmTransfer(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var result = await _transferService.ConfirmTransferAsync(userId, id, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Cancel a pending transfer before it has been confirmed and executed.
    /// </summary>
    /// <param name="id">The GUID of the pending transfer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cancellation confirmation details.</returns>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<CancelTransferResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<CancelTransferResponseDto>>> CancelTransfer(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var result = await _transferService.CancelTransferAsync(userId, id, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Retrieve full details of a specific transfer by its unique identifier.
    /// </summary>
    /// <param name="id">The GUID of the transfer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transfer details record.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TransferResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<TransferResponseDto>>> GetTransferById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var result = await _transferService.GetTransferByIdAsync(userId, id, cancellationToken);
        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Retrieve paginated list of transfers associated with the authenticated user's accounts.
    /// </summary>
    /// <param name="query">Filter parameters such as account, status, and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated transfer records.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TransferResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<PagedResult<TransferResponseDto>>>> GetTransfers(
        [FromQuery] TransferListQuery query,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var result = await _transferService.GetUserTransfersAsync(userId, query, cancellationToken);
        return Ok(result);
    }

    private string? GetRequiredUserId() =>
        string.IsNullOrWhiteSpace(_currentUserService.UserId) ? null : _currentUserService.UserId;
}
