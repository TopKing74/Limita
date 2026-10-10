using Limita.Domain.Enums;

namespace Limita.Application.Features.Recharge.Models;

public record MobileRechargeRequest(
    Guid AccountId,
    string PhoneNumber,
    MobileOperator Operator,
    decimal Amount);

public record MobileRechargeResponseDto(
    Guid Id,
    Guid AccountId,
    string PhoneNumber,
    string Operator,
    decimal Amount,
    string ReferenceNumber,
    Guid TransactionId,
    DateTime CreatedAt);
