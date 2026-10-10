using Limita.Application.Features.Cards.Models;

namespace Limita.Application.Features.Cards.Services;

public interface ICardService
{
    Task<IReadOnlyList<CardSummaryDto>> GetUserCardsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<CardDetailsDto?> GetCardByIdAsync(
        string userId,
        Guid cardId,
        CancellationToken cancellationToken = default);

    Task<CardDetailsDto> IssueCardAsync(
        string userId,
        IssueCardRequest request,
        CancellationToken cancellationToken = default);

    Task<CardDetailsDto?> BlockCardAsync(
        string userId,
        Guid cardId,
        BlockCardRequest request,
        CancellationToken cancellationToken = default);

    Task<CardDetailsDto?> UnblockCardAsync(
        string userId,
        Guid cardId,
        CancellationToken cancellationToken = default);

    Task<CardDetailsDto?> UpdateLimitsAsync(
        string userId,
        Guid cardId,
        UpdateCardLimitsRequest request,
        CancellationToken cancellationToken = default);

    Task<CardDetailsDto?> ChangePinAsync(
        string userId,
        Guid cardId,
        ChangeCardPinRequest request,
        CancellationToken cancellationToken = default);

    Task<CardDetailsDto?> SetDefaultCardAsync(
        string userId,
        Guid cardId,
        CancellationToken cancellationToken = default);
}
