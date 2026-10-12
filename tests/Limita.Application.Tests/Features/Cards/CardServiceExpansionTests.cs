using FluentAssertions;
using Moq;
using Moq.EntityFrameworkCore;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Cards.Models;
using Limita.Application.Features.Cards.Services;
using Limita.Domain.Entities;
using Limita.Domain.Enums;
using Xunit;

namespace Limita.Application.Tests.Features.Cards;

public class CardServiceExpansionTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<ICardPinHasher> _pinHasherMock;
    private readonly CardService _cardService;

    public CardServiceExpansionTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _pinHasherMock = new Mock<ICardPinHasher>();
        _cardService = new CardService(_contextMock.Object, _pinHasherMock.Object);
    }

    [Fact]
    public async Task GetUserCardsAsync_ShouldReturnOnlyUserCards_OrderedByDefaultAndCreatedAt()
    {
        // Arrange
        var userId = "user-1";
        var account1 = new Account { Id = Guid.NewGuid(), UserId = userId };
        var foreignAccount = new Account { Id = Guid.NewGuid(), UserId = "foreign-user" };

        var card1 = new BankCard
        {
            Id = Guid.NewGuid(),
            AccountId = account1.Id,
            Account = account1,
            MaskedNumber = "4000 **** **** 1111",
            IsDefault = false,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };

        var card2 = new BankCard
        {
            Id = Guid.NewGuid(),
            AccountId = account1.Id,
            Account = account1,
            MaskedNumber = "4000 **** **** 2222",
            IsDefault = true,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var foreignCard = new BankCard
        {
            Id = Guid.NewGuid(),
            AccountId = foreignAccount.Id,
            Account = foreignAccount,
            MaskedNumber = "5100 **** **** 9999"
        };

        _contextMock.Setup(c => c.BankCards)
            .ReturnsDbSet(new List<BankCard> { card1, card2, foreignCard });

        // Act
        var result = await _cardService.GetUserCardsAsync(userId);

        // Assert
        result.Should().HaveCount(2);
        result[0].MaskedNumber.Should().Be("4000 **** **** 2222"); // Default card first
        result[0].IsDefault.Should().BeTrue();
        result[1].MaskedNumber.Should().Be("4000 **** **** 1111");
        result.Should().NotContain(c => c.MaskedNumber == "5100 **** **** 9999");
    }

    [Fact]
    public async Task GetCardByIdAsync_ShouldReturnCard_WhenOwnedByUser()
    {
        // Arrange
        var userId = "user-1";
        var cardId = Guid.NewGuid();
        var account = new Account { Id = Guid.NewGuid(), UserId = userId, Currency = "EGP" };
        var card = new BankCard
        {
            Id = cardId,
            AccountId = account.Id,
            Account = account,
            MaskedNumber = "4000 **** **** 1234",
            Status = CardStatus.Active,
            DailyAtmLimit = 5000m,
            DailyPosLimit = 20000m
        };

        _contextMock.Setup(c => c.BankCards).ReturnsDbSet(new List<BankCard> { card });

        // Act
        var result = await _cardService.GetCardByIdAsync(userId, cardId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(cardId);
        result.MaskedNumber.Should().Be("4000 **** **** 1234");
        result.DailyAtmLimit.Should().Be(5000m);
    }

    [Fact]
    public async Task GetCardByIdAsync_ShouldReturnNull_WhenCardBelongsToAnotherUser()
    {
        // Arrange
        var cardId = Guid.NewGuid();
        var foreignAccount = new Account { Id = Guid.NewGuid(), UserId = "foreign-user" };
        var card = new BankCard
        {
            Id = cardId,
            AccountId = foreignAccount.Id,
            Account = foreignAccount,
            MaskedNumber = "4000 **** **** 1234"
        };

        _contextMock.Setup(c => c.BankCards).ReturnsDbSet(new List<BankCard> { card });

        // Act
        var result = await _cardService.GetCardByIdAsync("user-1", cardId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task IssueCardAsync_ShouldThrowKeyNotFoundException_WhenAccountNotOwned()
    {
        // Arrange
        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account>());

        var request = new IssueCardRequest(Guid.NewGuid(), CardType.Debit, CardForm.Virtual);

        // Act
        var act = async () => await _cardService.IssueCardAsync("user-1", request);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Account not found*");
    }

    [Fact]
    public async Task IssueCardAsync_ShouldSetPendingActivation_WhenCardFormIsPhysical()
    {
        // Arrange
        var userId = "user-1";
        var accountId = Guid.NewGuid();
        var account = new Account { Id = accountId, UserId = userId, Currency = "EGP", Status = AccountStatus.Active };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { account });
        var cards = new List<BankCard>();
        _contextMock.Setup(c => c.BankCards).ReturnsDbSet(cards);
        _contextMock.Setup(c => c.BankCards.AddAsync(It.IsAny<BankCard>(), It.IsAny<CancellationToken>()))
            .Callback<BankCard, CancellationToken>((c, _) => cards.Add(c));

        var request = new IssueCardRequest(accountId, CardType.Debit, CardForm.Physical, CardHolderName: "JOHN DOE");

        // Act
        var result = await _cardService.IssueCardAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.Form.Should().Be(CardForm.Physical);
        result.Status.Should().Be(CardStatus.PendingActivation);
    }

    [Fact]
    public async Task BlockCardAsync_ShouldSuccessfullyBlockCard_WhenOperable()
    {
        // Arrange
        var userId = "user-1";
        var cardId = Guid.NewGuid();
        var account = new Account { Id = Guid.NewGuid(), UserId = userId };
        var card = new BankCard
        {
            Id = cardId,
            AccountId = account.Id,
            Account = account,
            Status = CardStatus.Active
        };

        _contextMock.Setup(c => c.BankCards).ReturnsDbSet(new List<BankCard> { card });

        var request = new BlockCardRequest(CardBlockType.Temporary, "Suspicious transaction detected");

        // Act
        var result = await _cardService.BlockCardAsync(userId, cardId, request);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(CardStatus.Blocked);
        result.BlockType.Should().Be(CardBlockType.Temporary);
        result.BlockReason.Should().Be("Suspicious transaction detected");
        card.Status.Should().Be(CardStatus.Blocked);
        card.BlockedAt.Should().NotBeNull();

        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BlockCardAsync_ShouldThrowInvalidOperationException_WhenCardIsAlreadyPermanentlyBlocked()
    {
        // Arrange
        var userId = "user-1";
        var cardId = Guid.NewGuid();
        var account = new Account { Id = Guid.NewGuid(), UserId = userId };
        var card = new BankCard
        {
            Id = cardId,
            AccountId = account.Id,
            Account = account,
            Status = CardStatus.Blocked,
            BlockType = CardBlockType.Permanent
        };

        _contextMock.Setup(c => c.BankCards).ReturnsDbSet(new List<BankCard> { card });

        var request = new BlockCardRequest(CardBlockType.Permanent, "Re-blocking");

        // Act
        var act = async () => await _cardService.BlockCardAsync(userId, cardId, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*permanently blocked*");
    }

    [Fact]
    public async Task UnblockCardAsync_ShouldReactivateCard_WhenTemporarilyBlocked()
    {
        // Arrange
        var userId = "user-1";
        var cardId = Guid.NewGuid();
        var account = new Account { Id = Guid.NewGuid(), UserId = userId };
        var card = new BankCard
        {
            Id = cardId,
            AccountId = account.Id,
            Account = account,
            Status = CardStatus.Blocked,
            BlockType = CardBlockType.Temporary,
            BlockReason = "User requested freeze",
            BlockedAt = DateTime.UtcNow.AddHours(-1)
        };

        _contextMock.Setup(c => c.BankCards).ReturnsDbSet(new List<BankCard> { card });

        // Act
        var result = await _cardService.UnblockCardAsync(userId, cardId);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(CardStatus.Active);
        result.BlockType.Should().BeNull();
        result.BlockReason.Should().BeNull();

        card.Status.Should().Be(CardStatus.Active);
        card.BlockedAt.Should().BeNull();

        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnblockCardAsync_ShouldThrowInvalidOperationException_WhenCardIsPermanentlyBlocked()
    {
        // Arrange
        var userId = "user-1";
        var cardId = Guid.NewGuid();
        var account = new Account { Id = Guid.NewGuid(), UserId = userId };
        var card = new BankCard
        {
            Id = cardId,
            AccountId = account.Id,
            Account = account,
            Status = CardStatus.Blocked,
            BlockType = CardBlockType.Permanent
        };

        _contextMock.Setup(c => c.BankCards).ReturnsDbSet(new List<BankCard> { card });

        // Act
        var act = async () => await _cardService.UnblockCardAsync(userId, cardId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*permanently blocked*");
    }

    [Fact]
    public async Task UpdateLimitsAsync_ShouldUpdateAtnAndPosLimits_WhenCardOperable()
    {
        // Arrange
        var userId = "user-1";
        var cardId = Guid.NewGuid();
        var account = new Account { Id = Guid.NewGuid(), UserId = userId };
        var card = new BankCard
        {
            Id = cardId,
            AccountId = account.Id,
            Account = account,
            Status = CardStatus.Active,
            DailyAtmLimit = 5000m,
            DailyPosLimit = 20000m
        };

        _contextMock.Setup(c => c.BankCards).ReturnsDbSet(new List<BankCard> { card });

        var request = new UpdateCardLimitsRequest(10000m, 50000m);

        // Act
        var result = await _cardService.UpdateLimitsAsync(userId, cardId, request);

        // Assert
        result.Should().NotBeNull();
        result!.DailyAtmLimit.Should().Be(10000m);
        result.DailyPosLimit.Should().Be(50000m);

        card.DailyAtmLimit.Should().Be(10000m);
        card.DailyPosLimit.Should().Be(50000m);
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePinAsync_ShouldVerifyOldPinAndSaveHashedNewPin()
    {
        // Arrange
        var userId = "user-1";
        var cardId = Guid.NewGuid();
        var account = new Account { Id = Guid.NewGuid(), UserId = userId };
        var card = new BankCard
        {
            Id = cardId,
            AccountId = account.Id,
            Account = account,
            Status = CardStatus.Active,
            PinHash = "OLD_HASH"
        };

        _contextMock.Setup(c => c.BankCards).ReturnsDbSet(new List<BankCard> { card });

        _pinHasherMock.Setup(p => p.Verify("1234", "OLD_HASH")).Returns(true);
        _pinHasherMock.Setup(p => p.Hash("5678")).Returns("NEW_HASH");

        var request = new ChangeCardPinRequest("5678", "1234");

        // Act
        var result = await _cardService.ChangePinAsync(userId, cardId, request);

        // Assert
        result.Should().NotBeNull();
        result!.IsPinSet.Should().BeTrue();
        card.PinHash.Should().Be("NEW_HASH");

        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePinAsync_ShouldThrowInvalidOperationException_WhenCurrentPinIsIncorrect()
    {
        // Arrange
        var userId = "user-1";
        var cardId = Guid.NewGuid();
        var account = new Account { Id = Guid.NewGuid(), UserId = userId };
        var card = new BankCard
        {
            Id = cardId,
            AccountId = account.Id,
            Account = account,
            Status = CardStatus.Active,
            PinHash = "REAL_HASH"
        };

        _contextMock.Setup(c => c.BankCards).ReturnsDbSet(new List<BankCard> { card });

        _pinHasherMock.Setup(p => p.Verify("0000", "REAL_HASH")).Returns(false); // Incorrect

        var request = new ChangeCardPinRequest("5678", "0000");

        // Act
        var act = async () => await _cardService.ChangePinAsync(userId, cardId, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Current PIN is incorrect*");
    }
}
