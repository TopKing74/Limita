using Microsoft.EntityFrameworkCore;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Cards.Models;
using Limita.Domain.Entities;
using Limita.Domain.Enums;

namespace Limita.Application.Features.Cards.Services;

public class CardService : ICardService
{
    private readonly IApplicationDbContext _context;
    private readonly ICardPinHasher _pinHasher;

    public CardService(IApplicationDbContext context, ICardPinHasher pinHasher)
    {
        _context = context;
        _pinHasher = pinHasher;
    }

    public async Task<IReadOnlyList<CardSummaryDto>> GetUserCardsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {

        return await OwnedCardsQuery(userId)
            .AsNoTracking()
            .OrderByDescending(c => c.IsDefault)
            .ThenByDescending(c => c.CreatedAt)
            .Select(c => new CardSummaryDto(
                c.Id,
                c.AccountId,
                c.MaskedNumber,
                c.Type,
                c.Form,
                c.Status,
                c.IsDefault,
                c.ExpiryMonth,
                c.ExpiryYear))
            .ToListAsync(cancellationToken);
    }

    public async Task<CardDetailsDto?> GetCardByIdAsync(
        string userId,
        Guid cardId,
        CancellationToken cancellationToken = default)
    {
        var card = await OwnedCardsQuery(userId)
            .AsNoTracking()
            .Include(c => c.Account)
            .FirstOrDefaultAsync(c => c.Id == cardId, cancellationToken);

        return card is null ? null : MapToDetails(card);
    }

    public async Task<CardDetailsDto> IssueCardAsync(
        string userId,
        IssueCardRequest request,
        CancellationToken cancellationToken = default)
    {
        var account = await _context.Accounts
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == request.AccountId && a.UserId == userId, cancellationToken);

        if (account is null)
        {
            throw new KeyNotFoundException("Account not found or you do not have access to it.");
        }

        if (account.Status != AccountStatus.Active)
        {
            throw new InvalidOperationException("Cards can only be issued for active accounts.");
        }

        var holderName = string.IsNullOrWhiteSpace(request.CardHolderName)
            ? (account.User?.FullName ?? "CARD HOLDER").ToUpperInvariant()
            : request.CardHolderName.Trim().ToUpperInvariant();

        var (atmLimit, posLimit) = GetDefaultLimits(request.Type, account.Currency);
        var now = DateTime.UtcNow;

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        try
        {
            if (_context.Database != null && _context.Database.CurrentTransaction == null)
            {
                transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            }
        }
        catch (InvalidOperationException) { }
        catch (NotSupportedException) { }

        try
        {
            if (request.IsDefault)
            {
                await ClearDefaultFlagsAsync(account.Id, null, cancellationToken);
            }

            var card = new BankCard
            {
                AccountId = account.Id,
                CardHolderName = holderName,
                MaskedNumber = GenerateMaskedNumber(request.Type),
                ExpiryMonth = now.Month,
                ExpiryYear = now.Year + 3,
                Type = request.Type,
                Form = request.Form,
                Status = request.Form == CardForm.Virtual
                    ? CardStatus.Active
                    : CardStatus.PendingActivation,
                IsDefault = request.IsDefault,
                DailyAtmLimit = atmLimit,
                DailyPosLimit = posLimit,
                CreatedAt = now
            };

            await _context.BankCards.AddAsync(card, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            card.Account = account;
            return MapToDetails(card);
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task<CardDetailsDto?> BlockCardAsync(
        string userId,
        Guid cardId,
        BlockCardRequest request,
        CancellationToken cancellationToken = default)
    {
        var card = await GetOwnedCardTrackedAsync(userId, cardId, cancellationToken);
        if (card is null)
        {
            return null;
        }

        if (card.Status == CardStatus.Expired)
        {
            throw new InvalidOperationException("An expired card cannot be blocked.");
        }

        if (card.Status == CardStatus.Blocked && card.BlockType == CardBlockType.Permanent)
        {
            throw new InvalidOperationException("Card is already permanently blocked.");
        }

        card.Status = CardStatus.Blocked;
        card.BlockType = request.BlockType;
        card.BlockReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        card.BlockedAt = DateTime.UtcNow;
        card.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return MapToDetails(card);
    }

    public async Task<CardDetailsDto?> UnblockCardAsync(
        string userId,
        Guid cardId,
        CancellationToken cancellationToken = default)
    {
        var card = await GetOwnedCardTrackedAsync(userId, cardId, cancellationToken);
        if (card is null)
        {
            return null;
        }

        if (card.Status != CardStatus.Blocked)
        {
            throw new InvalidOperationException("Only blocked cards can be unblocked.");
        }

        if (card.BlockType == CardBlockType.Permanent)
        {
            throw new InvalidOperationException("A permanently blocked card cannot be reactivated. Request a replacement card instead.");
        }

        card.Status = CardStatus.Active;
        card.BlockType = null;
        card.BlockReason = null;
        card.BlockedAt = null;
        card.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return MapToDetails(card);
    }

    public async Task<CardDetailsDto?> UpdateLimitsAsync(
        string userId,
        Guid cardId,
        UpdateCardLimitsRequest request,
        CancellationToken cancellationToken = default)
    {
        var card = await GetOwnedCardTrackedAsync(userId, cardId, cancellationToken);
        if (card is null)
        {
            return null;
        }

        EnsureCardIsOperable(card);

        card.DailyAtmLimit = request.DailyAtmLimit;
        card.DailyPosLimit = request.DailyPosLimit;
        card.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return MapToDetails(card);
    }

    public async Task<CardDetailsDto?> ChangePinAsync(
        string userId,
        Guid cardId,
        ChangeCardPinRequest request,
        CancellationToken cancellationToken = default)
    {
        var card = await GetOwnedCardTrackedAsync(userId, cardId, cancellationToken);
        if (card is null)
        {
            return null;
        }

        EnsureCardIsOperable(card);

        if (card.IsPinSet)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPin))
            {
                throw new InvalidOperationException("Current PIN is required to change an existing PIN.");
            }

            if (!_pinHasher.Verify(request.CurrentPin, card.PinHash!))
            {
                throw new InvalidOperationException("Current PIN is incorrect.");
            }
        }

        if (request.NewPin == request.CurrentPin)
        {
            throw new InvalidOperationException("New PIN must be different from the current PIN.");
        }

        card.PinHash = _pinHasher.Hash(request.NewPin);
        card.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return MapToDetails(card);
    }

    public async Task<CardDetailsDto?> SetDefaultCardAsync(
        string userId,
        Guid cardId,
        CancellationToken cancellationToken = default)
    {
        var card = await GetOwnedCardTrackedAsync(userId, cardId, cancellationToken);
        if (card is null)
        {
            return null;
        }

        EnsureCardIsOperable(card);

        if (card.IsDefault)
        {
            return MapToDetails(card);
        }

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        try
        {
            if (_context.Database != null && _context.Database.CurrentTransaction == null)
            {
                transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            }
        }
        catch (InvalidOperationException) { }
        catch (NotSupportedException) { }

        try
        {
            await ClearDefaultFlagsAsync(card.AccountId, card.Id, cancellationToken);

            card.IsDefault = true;
            card.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return MapToDetails(card);
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private IQueryable<BankCard> OwnedCardsQuery(string userId) =>
        _context.BankCards.Where(c => c.Account!.UserId == userId);

    private async Task<BankCard?> GetOwnedCardTrackedAsync(
        string userId,
        Guid cardId,
        CancellationToken cancellationToken)
    {
        return await OwnedCardsQuery(userId)
            .Include(c => c.Account)
            .FirstOrDefaultAsync(c => c.Id == cardId, cancellationToken);
    }

    private async Task ClearDefaultFlagsAsync(Guid accountId, Guid? excludeCardId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.BankCards
            .Where(c => c.AccountId == accountId && c.IsDefault);

        if (excludeCardId.HasValue)
        {
            query = query.Where(c => c.Id != excludeCardId.Value);
        }

        var defaults = await query.ToListAsync(cancellationToken);

        foreach (var existing in defaults)
        {
            existing.IsDefault = false;
            existing.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static void EnsureCardIsOperable(BankCard card)
    {
        if (card.Status == CardStatus.Blocked)
        {
            throw new InvalidOperationException("Operation is not allowed on a blocked card. Unblock it first.");
        }

        if (card.Status == CardStatus.Expired)
        {
            throw new InvalidOperationException("Operation is not allowed on an expired card.");
        }
    }

    private static string GenerateMaskedNumber(CardType type)
    {
        var bin = type switch
        {
            CardType.Credit => "5100",
            CardType.Prepaid => "6200",
            _ => "4000"
        };

        var lastFour = Random.Shared.Next(0, 10000).ToString("D4");
        return $"{bin} **** **** {lastFour}";
    }

    private static (decimal Atm, decimal Pos) GetDefaultLimits(CardType type, string currency)
    {
        return (type, currency.ToUpperInvariant()) switch
        {
            (CardType.Credit, "EGP") => (10000m, 50000m),
            (CardType.Credit, _) => (500m, 2500m),
            (CardType.Prepaid, "EGP") => (2000m, 10000m),
            (CardType.Prepaid, _) => (200m, 1000m),
            (_, "USD") or (_, "EUR") => (300m, 1500m),
            _ => (5000m, 20000m)
        };
    }

    private static CardDetailsDto MapToDetails(BankCard card) =>
        new(
            card.Id,
            card.AccountId,
            card.Account?.AccountNumber ?? string.Empty,
            card.CardHolderName,
            card.MaskedNumber,
            card.Type,
            card.Form,
            card.Status,
            card.IsDefault,
            card.ExpiryMonth,
            card.ExpiryYear,
            card.DailyAtmLimit,
            card.DailyPosLimit,
            card.IsPinSet,
            card.BlockType,
            card.BlockReason,
            card.BlockedAt,
            card.CreatedAt,
            card.UpdatedAt);
}
