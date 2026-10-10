using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Moq.EntityFrameworkCore;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Accounts.Models;
using Limita.Application.Features.Accounts.Services;
using Limita.Domain.Entities;
using Limita.Domain.Enums;
using Xunit;

namespace Limita.Application.Tests.Features.Accounts;

public class AccountServiceTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly AccountService _accountService;

    public AccountServiceTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _accountService = new AccountService(_contextMock.Object);
    }

    [Fact]
    public async Task WithdrawAsync_ShouldReduceBalanceAndRecordTransaction_WhenSuccessful()
    {
        // Arrange
        var userId = "user-1";
        var accountId = Guid.NewGuid();
        var account = new Account
        {
            Id = accountId,
            UserId = userId,
            Balance = 1000m,
            Currency = "USD",
            Status = AccountStatus.Active
        };

        var dbAccounts = new List<Account> { account };
        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(dbAccounts);

        var dbTransactions = new List<Transaction>();
        _contextMock.Setup(c => c.Transactions).ReturnsDbSet(dbTransactions);

        var request = new WithdrawalRequest(200m);

        // Act
        var result = await _accountService.WithdrawAsync(userId, accountId, request);

        // Assert
        account.Balance.Should().Be(800m);
        
        _contextMock.Verify(c => c.Transactions.AddAsync(It.Is<Transaction>(t => 
            t.AccountId == accountId &&
            t.Amount == 200m &&
            t.Type == TransactionType.Withdrawal &&
            t.Status == TransactionStatus.Completed &&
            t.Currency == "USD"
        ), It.IsAny<CancellationToken>()), Times.Once);

        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        result.Should().NotBeNull();
        result.Amount.Should().Be(200m);
        result.Type.Should().Be(TransactionType.Withdrawal);
    }

    [Fact]
    public async Task WithdrawAsync_ShouldThrowInvalidOperationException_WhenInsufficientBalance()
    {
        // Arrange
        var userId = "user-1";
        var accountId = Guid.NewGuid();
        var account = new Account
        {
            Id = accountId,
            UserId = userId,
            Balance = 100m, // Balance is less than requested amount
            Status = AccountStatus.Active
        };

        var dbAccounts = new List<Account> { account };
        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(dbAccounts);

        var request = new WithdrawalRequest(200m);

        // Act
        var action = async () => await _accountService.WithdrawAsync(userId, accountId, request);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Insufficient available balance.");
    }

    [Fact]
    public async Task WithdrawAsync_ShouldThrowKeyNotFoundException_WhenAccountNotFound()
    {
        // Arrange
        var dbAccounts = new List<Account>();
        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(dbAccounts);

        var request = new WithdrawalRequest(200m);

        // Act
        var action = async () => await _accountService.WithdrawAsync("user-1", Guid.NewGuid(), request);

        // Assert
        await action.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task WithdrawAsync_ShouldThrowInvalidOperationException_WhenAccountIsInactive()
    {
        // Arrange
        var userId = "user-1";
        var accountId = Guid.NewGuid();
        var account = new Account
        {
            Id = accountId,
            UserId = userId,
            Balance = 1000m,
            Status = AccountStatus.Suspended // Inactive status
        };

        var dbAccounts = new List<Account> { account };
        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(dbAccounts);

        var request = new WithdrawalRequest(200m);

        // Act
        var action = async () => await _accountService.WithdrawAsync(userId, accountId, request);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot withdraw from an inactive or blocked account.");
    }
}
