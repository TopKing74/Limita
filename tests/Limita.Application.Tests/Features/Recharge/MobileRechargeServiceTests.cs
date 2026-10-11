using FluentAssertions;
using Moq;
using Moq.EntityFrameworkCore;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Recharge.Models;
using Limita.Application.Features.Recharge.Services;
using Limita.Domain.Entities;
using Limita.Domain.Enums;
using Xunit;

namespace Limita.Application.Tests.Features.Recharge;

public class MobileRechargeServiceTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly MobileRechargeService _rechargeService;

    public MobileRechargeServiceTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _rechargeService = new MobileRechargeService(_contextMock.Object);
    }

    [Fact]
    public async Task RechargeMobileAsync_ShouldDebitAccount_CreateRechargeAndLedgerTransaction_WhenSuccessful()
    {
        // Arrange
        var userId = "user-1";
        var accountId = Guid.NewGuid();
        var account = new Account
        {
            Id = accountId,
            UserId = userId,
            Balance = 500m,
            Currency = "EGP",
            Status = AccountStatus.Active
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { account });
        _contextMock.Setup(c => c.MobileRecharges).ReturnsDbSet(new List<MobileRecharge>());
        _contextMock.Setup(c => c.Transactions).ReturnsDbSet(new List<Transaction>());

        var request = new MobileRechargeRequest(
            AccountId: accountId,
            PhoneNumber: "01012345678",
            Operator: MobileOperator.Vodafone,
            Amount: 100m);

        // Act
        var result = await _rechargeService.RechargeMobileAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.PhoneNumber.Should().Be("01012345678");
        result.Operator.Should().Be("Vodafone");
        result.Amount.Should().Be(100m);
        result.ReferenceNumber.Should().StartWith("MRC-");

        // Verify account balance was debited
        account.Balance.Should().Be(400m);

        // Verify MobileRecharge entity was added
        _contextMock.Verify(c => c.MobileRecharges.AddAsync(It.Is<MobileRecharge>(r =>
            r.AccountId == accountId &&
            r.PhoneNumber == "01012345678" &&
            r.OperatorName == "Vodafone" &&
            r.Amount == 100m
        ), It.IsAny<CancellationToken>()), Times.Once);

        // Verify Transaction ledger tracking entry was created with MobileRecharge type
        _contextMock.Verify(c => c.Transactions.AddAsync(It.Is<Transaction>(t =>
            t.AccountId == accountId &&
            t.Amount == 100m &&
            t.Type == TransactionType.MobileRecharge &&
            t.Status == TransactionStatus.Completed &&
            t.Currency == "EGP"
        ), It.IsAny<CancellationToken>()), Times.Once);

        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RechargeMobileAsync_ShouldThrowKeyNotFoundException_WhenAccountNotFoundOrNotOwned()
    {
        // Arrange
        var userId = "user-1";
        var foreignAccountId = Guid.NewGuid();
        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account>());

        var request = new MobileRechargeRequest(
            AccountId: foreignAccountId,
            PhoneNumber: "01012345678",
            Operator: MobileOperator.Vodafone,
            Amount: 50m);

        // Act
        var act = async () => await _rechargeService.RechargeMobileAsync(userId, request);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Account not found*");
    }

    [Fact]
    public async Task RechargeMobileAsync_ShouldThrowInvalidOperationException_WhenAccountInactive()
    {
        // Arrange
        var userId = "user-1";
        var accountId = Guid.NewGuid();
        var account = new Account
        {
            Id = accountId,
            UserId = userId,
            Balance = 500m,
            Status = AccountStatus.Frozen
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { account });

        var request = new MobileRechargeRequest(
            AccountId: accountId,
            PhoneNumber: "01012345678",
            Operator: MobileOperator.Vodafone,
            Amount: 50m);

        // Act
        var act = async () => await _rechargeService.RechargeMobileAsync(userId, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*inactive or blocked*");
    }

    [Fact]
    public async Task RechargeMobileAsync_ShouldThrowInvalidOperationException_WhenInsufficientBalance()
    {
        // Arrange
        var userId = "user-1";
        var accountId = Guid.NewGuid();
        var account = new Account
        {
            Id = accountId,
            UserId = userId,
            Balance = 30m,
            Status = AccountStatus.Active
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { account });

        var request = new MobileRechargeRequest(
            AccountId: accountId,
            PhoneNumber: "01012345678",
            Operator: MobileOperator.Vodafone,
            Amount: 100m);

        // Act
        var act = async () => await _rechargeService.RechargeMobileAsync(userId, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Insufficient available balance*");
    }

    [Fact]
    public async Task RechargeMobileAsync_ShouldTriggerNotification_WhenSuccessfulAndNotificationServiceProvided()
    {
        // Arrange
        var userId = "user-1";
        var accountId = Guid.NewGuid();
        var account = new Account
        {
            Id = accountId,
            UserId = userId,
            Balance = 500m,
            Currency = "EGP",
            Status = AccountStatus.Active
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { account });
        _contextMock.Setup(c => c.MobileRecharges).ReturnsDbSet(new List<MobileRecharge>());
        _contextMock.Setup(c => c.Transactions).ReturnsDbSet(new List<Transaction>());

        var notificationServiceMock = new Mock<Limita.Application.Features.Notifications.Services.INotificationService>();
        var serviceWithNotifications = new MobileRechargeService(_contextMock.Object, notificationServiceMock.Object);

        var request = new MobileRechargeRequest(
            AccountId: accountId,
            PhoneNumber: "01012345678",
            Operator: MobileOperator.Vodafone,
            Amount: 100m);

        // Act
        var result = await serviceWithNotifications.RechargeMobileAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        notificationServiceMock.Verify(n => n.CreateAndSendNotificationAsync(
            userId,
            "Mobile Recharge Completed",
            It.Is<string>(msg => msg.Contains("01012345678") && msg.Contains("Vodafone")),
            "Transaction",
            result.ReferenceNumber,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
