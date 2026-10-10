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

public class CardServiceTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<ICardPinHasher> _pinHasherMock;
    private readonly CardService _cardService;

    public CardServiceTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _pinHasherMock = new Mock<ICardPinHasher>();
        _cardService = new CardService(_contextMock.Object, _pinHasherMock.Object);
    }

    [Fact]
    public async Task SetDefaultCardAsync_ShouldTogglePreviousDefaultToFalse_WhenSettingNewDefaultCard()
    {
        // Arrange
        var userId = "user-1";
        var accountId = Guid.NewGuid();
        var account = new Account { Id = accountId, UserId = userId, Currency = "EGP", Status = AccountStatus.Active };

        var card1 = new BankCard
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Account = account,
            MaskedNumber = "4000 **** **** 1111",
            IsDefault = true,
            Status = CardStatus.Active
        };

        var card2 = new BankCard
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Account = account,
            MaskedNumber = "4000 **** **** 2222",
            IsDefault = false,
            Status = CardStatus.Active
        };

        var cards = new List<BankCard> { card1, card2 };

        _contextMock.Setup(c => c.BankCards).ReturnsDbSet(cards);

        // Act
        var result = await _cardService.SetDefaultCardAsync(userId, card2.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(card2.Id);
        result.IsDefault.Should().BeTrue();

        card1.IsDefault.Should().BeFalse();
        card2.IsDefault.Should().BeTrue();

        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IssueCardAsync_ShouldTogglePreviousDefaultToFalse_WhenIssuedWithIsDefaultTrue()
    {
        // Arrange
        var userId = "user-1";
        var accountId = Guid.NewGuid();
        var account = new Account
        {
            Id = accountId,
            UserId = userId,
            Currency = "EGP",
            Status = AccountStatus.Active
        };

        var existingDefaultCard = new BankCard
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Account = account,
            MaskedNumber = "4000 **** **** 1111",
            IsDefault = true,
            Status = CardStatus.Active
        };

        var accounts = new List<Account> { account };
        var cards = new List<BankCard> { existingDefaultCard };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(accounts);
        _contextMock.Setup(c => c.BankCards).ReturnsDbSet(cards);

        var request = new IssueCardRequest(
            AccountId: accountId,
            Type: CardType.Debit,
            Form: CardForm.Virtual,
            IsDefault: true);

        // Act
        var result = await _cardService.IssueCardAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.IsDefault.Should().BeTrue();

        // The previous default card must be toggled to false
        existingDefaultCard.IsDefault.Should().BeFalse();

        _contextMock.Verify(c => c.BankCards.AddAsync(It.Is<BankCard>(c => c.IsDefault == true), It.IsAny<CancellationToken>()), Times.Once);
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetDefaultCardAsync_ShouldReturnNull_WhenCardNotFound()
    {
        // Arrange
        var userId = "user-1";
        _contextMock.Setup(c => c.BankCards).ReturnsDbSet(new List<BankCard>());

        // Act
        var result = await _cardService.SetDefaultCardAsync(userId, Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetDefaultCardAsync_ShouldThrowInvalidOperationException_WhenCardIsBlocked()
    {
        // Arrange
        var userId = "user-1";
        var accountId = Guid.NewGuid();
        var account = new Account { Id = accountId, UserId = userId, Status = AccountStatus.Active };
        var card = new BankCard
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Account = account,
            Status = CardStatus.Blocked,
            IsDefault = false
        };

        _contextMock.Setup(c => c.BankCards).ReturnsDbSet(new List<BankCard> { card });

        // Act
        var act = async () => await _cardService.SetDefaultCardAsync(userId, card.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*blocked*");
    }

    [Fact]
    public async Task SetDefaultCardAsync_ShouldReturnDetailsImmediately_WhenCardIsAlreadyDefault()
    {
        // Arrange
        var userId = "user-1";
        var accountId = Guid.NewGuid();
        var account = new Account { Id = accountId, UserId = userId, Status = AccountStatus.Active };
        var card = new BankCard
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Account = account,
            Status = CardStatus.Active,
            IsDefault = true
        };

        _contextMock.Setup(c => c.BankCards).ReturnsDbSet(new List<BankCard> { card });

        // Act
        var result = await _cardService.SetDefaultCardAsync(userId, card.Id);

        // Assert
        result.Should().NotBeNull();
        result!.IsDefault.Should().BeTrue();
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
