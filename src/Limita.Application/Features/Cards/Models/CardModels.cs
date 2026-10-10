using Limita.Domain.Enums;

namespace Limita.Application.Features.Cards.Models;

public record IssueCardRequest(
    Guid AccountId,
    CardType Type,
    CardForm Form,
    bool IsDefault = false,
    string? CardHolderName = null);

public record BlockCardRequest(
    CardBlockType BlockType,
    string? Reason = null);

public record UpdateCardLimitsRequest(
    decimal DailyAtmLimit,
    decimal DailyPosLimit);

public record ChangeCardPinRequest(
    string NewPin,
    string? CurrentPin = null);

public record CardSummaryDto(
    Guid Id,
    Guid AccountId,
    string MaskedNumber,
    CardType Type,
    CardForm Form,
    CardStatus Status,
    bool IsDefault,
    int ExpiryMonth,
    int ExpiryYear);

public record CardDetailsDto(
    Guid Id,
    Guid AccountId,
    string AccountNumber,
    string CardHolderName,
    string MaskedNumber,
    CardType Type,
    CardForm Form,
    CardStatus Status,
    bool IsDefault,
    int ExpiryMonth,
    int ExpiryYear,
    decimal DailyAtmLimit,
    decimal DailyPosLimit,
    bool IsPinSet,
    CardBlockType? BlockType,
    string? BlockReason,
    DateTime? BlockedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
