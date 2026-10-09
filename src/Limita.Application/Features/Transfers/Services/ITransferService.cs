using Limita.Application.Common;
using Limita.Application.Features.Transfers.Models;

namespace Limita.Application.Features.Transfers.Services;

/// <summary>
/// Service contract defining operations for initiating and processing bank money transfers.
/// </summary>
public interface ITransferService
{
    /// <summary>
    /// Validates transfer criteria and initiates a pending transfer and corresponding ledger entry.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user requesting the transfer.</param>
    /// <param name="request">Transfer initiation parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success with transfer review details, or failure with validation message.</returns>
    Task<ApiResponse<TransferResponseDto>> CreateTransferAsync(
        string userId,
        CreateTransferRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically confirms and executes a pending transfer, debiting the sender, crediting the recipient, and completing ledger records.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="transferId">The GUID of the pending transfer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Execution receipt with updated balances and status.</returns>
    Task<ApiResponse<ConfirmTransferResponseDto>> ConfirmTransferAsync(
        string userId,
        Guid transferId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a pending transfer before it has been confirmed and executed.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="transferId">The GUID of the pending transfer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cancellation confirmation details.</returns>
    Task<ApiResponse<CancelTransferResponseDto>> CancelTransferAsync(
        string userId,
        Guid transferId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves full transfer details by transfer identifier for authorized account holders.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="transferId">The GUID of the transfer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Full transfer record details.</returns>
    Task<ApiResponse<TransferResponseDto>> GetTransferByIdAsync(
        string userId,
        Guid transferId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves paginated transfers for the authenticated user with optional account and status filters.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="query">Filter parameters such as account, status, and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated transfer records.</returns>
    Task<ApiResponse<PagedResult<TransferResponseDto>>> GetUserTransfersAsync(
        string userId,
        TransferListQuery query,
        CancellationToken cancellationToken = default);
}
