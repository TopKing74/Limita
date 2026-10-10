using Limita.Domain.Enums;

namespace Limita.Application.Features.Bills.Models;

public record CreateBillRequest(
    Guid ProviderId,
    string BillingNumber,
    decimal Amount,
    DateTime DueDate
);

public record BillDto(
    Guid Id,
    Guid ProviderId,
    string ProviderName,
    string BillingNumber,
    decimal Amount,
    DateTime DueDate,
    BillStatus Status,
    DateTime CreatedAt
);

public record PayBillRequest(Guid AccountId);

public record BillPaymentResponseDto(
    Guid BillId,
    Guid TransactionId,
    string ReferenceNumber,
    decimal AmountPaid,
    DateTime PaymentDate
);
