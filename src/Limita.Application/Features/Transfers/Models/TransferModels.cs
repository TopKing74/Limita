using Limita.Domain.Enums;

namespace Limita.Application.Features.Transfers.Models;

/// <summary>
/// Request payload for creating a new money transfer between accounts.
/// </summary>
/// <param name="SourceAccountId">The GUID of the sender's bank account.</param>
/// <param name="DestinationAccountNumber">The bank account number of the recipient.</param>
/// <param name="Amount">The amount of money to transfer (must be greater than 0).</param>
/// <param name="Description">Optional transfer reference or narrative.</param>
public record CreateTransferRequest(
    Guid SourceAccountId,
    string DestinationAccountNumber,
    decimal Amount,
    string? Description = null);

/// <summary>
/// Response payload returned after creating or reviewing a transfer.
/// </summary>
public record TransferResponseDto(
    Guid Id,
    Guid TransactionId,
    string ReferenceNumber,
    Guid SourceAccountId,
    string SourceAccountNumber,
    Guid DestinationAccountId,
    string DestinationAccountNumber,
    string DestinationAccountHolderName,
    decimal Amount,
    decimal Fee,
    decimal TotalDeduction,
    string Currency,
    TransferStatus Status,
    string StatusName,
    string? Description,
    DateTime CreatedAt,
    string Message);

/// <summary>
/// Final execution receipt returned after atomically confirming a bank transfer.
/// </summary>
public record ConfirmTransferResponseDto(
    Guid TransferId,
    Guid TransactionId,
    string ReferenceNumber,
    string SourceAccountNumber,
    string DestinationAccountNumber,
    string RecipientName,
    decimal TransferredAmount,
    decimal Fee,
    decimal TotalDeducted,
    decimal RemainingBalance,
    string Currency,
    TransferStatus Status,
    DateTime ExecutedAt,
    string Message);

/// <summary>
/// Response payload returned upon cancelling a pending transfer.
/// </summary>
public record CancelTransferResponseDto(
    Guid TransferId,
    Guid TransactionId,
    string ReferenceNumber,
    TransferStatus Status,
    DateTime CancelledAt,
    string Message);

/// <summary>
/// Query filter and pagination parameters for retrieving transfers list.
/// </summary>
public record TransferListQuery(
    Guid? AccountId = null,
    TransferStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 10);

