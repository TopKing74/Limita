using FluentAssertions;
using Moq;
using Moq.EntityFrameworkCore;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Accounts.Models;
using Limita.Application.Features.Accounts.Services;
using Limita.Domain.Entities;
using Limita.Domain.Enums;
using Xunit;

namespace Limita.Application.Tests.Features.Accounts;

public class AccountServiceExpansionTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly AccountService _accountService;

    public AccountServiceExpansionTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _accountService = new AccountService(_contextMock.Object);
    }

    [Theory]
    [InlineData("EGP", "EG")]
    [InlineData("USD", "US")]
    [InlineData("EUR", "EU")]
    public async Task CreateSecondaryAccountAsync_ShouldSetCorrectCurrencyPrefixAndActiveStatus(string currency, string expectedPrefix)
    {
        // Arrange
        var userId = "user-test-1";
        var accounts = new List<Account>();
        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(accounts);
        _contextMock.Setup(c => c.Accounts.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
            .Callback<Account, CancellationToken>((a, _) => accounts.Add(a));

        var request = new CreateAccountRequest(AccountType.Savings, currency);

        // Act
        var result = await _accountService.CreateSecondaryAccountAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.Currency.Should().Be(currency);
        result.AccountNumber.Should().StartWith(expectedPrefix);
        result.Balance.Should().Be(0m);
        result.Status.Should().Be(AccountStatus.Active);
        result.Type.Should().Be(AccountType.Savings);

        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUserAccountsAsync_ShouldReturnOnlyUserAccounts_OrderedByCreatedAtDesc()
    {
        // Arrange
        var userId = "user-test-1";
        var a1 = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = "EG1001",
            Currency = "EGP",
            Balance = 1000m,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };
        var a2 = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = "US1002",
            Currency = "USD",
            Balance = 500m,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var foreignAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = "foreign-user",
            AccountNumber = "EG9999",
            Balance = 50000m
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { a1, a2, foreignAccount });

        // Act
        var result = await _accountService.GetUserAccountsAsync(userId);

        // Assert
        result.Should().HaveCount(2);
        result[0].AccountNumber.Should().Be("US1002"); // Newer first
        result[1].AccountNumber.Should().Be("EG1001");
        result.Should().NotContain(a => a.AccountNumber == "EG9999");
    }

    [Fact]
    public async Task GetAccountByIdAsync_ShouldReturnDetails_WhenOwnedByUser()
    {
        // Arrange
        var userId = "user-test-1";
        var accountId = Guid.NewGuid();
        var account = new Account
        {
            Id = accountId,
            UserId = userId,
            AccountNumber = "EG1001",
            Currency = "EGP",
            Balance = 1000m,
            DailyLimit = 50000m,
            MonthlyLimit = 250000m,
            PerTransactionLimit = 10000m,
            Status = AccountStatus.Active
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { account });

        // Act
        var result = await _accountService.GetAccountByIdAsync(userId, accountId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(accountId);
        result.AccountNumber.Should().Be("EG1001");
        result.DailyLimit.Should().Be(50000m);
        result.PerTransactionLimit.Should().Be(10000m);
    }

    [Fact]
    public async Task GetAccountByIdAsync_ShouldReturnNull_WhenBelongsToAnotherUser()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var foreignAccount = new Account
        {
            Id = accountId,
            UserId = "different-user",
            AccountNumber = "EG9999"
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { foreignAccount });

        // Act
        var result = await _accountService.GetAccountByIdAsync("user-test-1", accountId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAccountBalanceAsync_ShouldReturnBalanceDto_WhenOwnedByUser()
    {
        // Arrange
        var userId = "user-test-1";
        var accountId = Guid.NewGuid();
        var account = new Account
        {
            Id = accountId,
            UserId = userId,
            AccountNumber = "EG1001",
            Currency = "EGP",
            Balance = 7500.50m,
            Status = AccountStatus.Active
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { account });

        // Act
        var result = await _accountService.GetAccountBalanceAsync(userId, accountId);

        // Assert
        result.Should().NotBeNull();
        result!.AccountId.Should().Be(accountId);
        result.Balance.Should().Be(7500.50m);
        result.Currency.Should().Be("EGP");
        result.Status.Should().Be(AccountStatus.Active);
    }

    [Fact]
    public async Task DepositAsync_ShouldIncreaseBalanceAndRecordCompletedTransaction()
    {
        // Arrange
        var userId = "user-test-1";
        var accountId = Guid.NewGuid();
        var account = new Account
        {
            Id = accountId,
            UserId = userId,
            AccountNumber = "EG1001",
            Currency = "EGP",
            Balance = 1000m,
            Status = AccountStatus.Active
        };

        var transactions = new List<Transaction>();
        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { account });
        _contextMock.Setup(c => c.Transactions).ReturnsDbSet(transactions);
        _contextMock.Setup(c => c.Transactions.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .Callback<Transaction, CancellationToken>((t, _) => transactions.Add(t));

        var request = new DepositRequest(500m);

        // Act
        var result = await _accountService.DepositAsync(userId, accountId, request);

        // Assert
        result.Should().NotBeNull();
        result.Amount.Should().Be(500m);
        result.Type.Should().Be(TransactionType.Deposit);
        result.Status.Should().Be(TransactionStatus.Completed);
        account.Balance.Should().Be(1500m);

        transactions.Should().ContainSingle();
        transactions[0].Amount.Should().Be(500m);
        transactions[0].ReferenceNumber.Should().StartWith("DEP-");

        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DepositAsync_ShouldThrowInvalidOperationException_WhenAccountInactive()
    {
        // Arrange
        var userId = "user-test-1";
        var accountId = Guid.NewGuid();
        var account = new Account
        {
            Id = accountId,
            UserId = userId,
            Balance = 1000m,
            Status = AccountStatus.Suspended
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { account });

        var request = new DepositRequest(500m);

        // Act
        var act = async () => await _accountService.DepositAsync(userId, accountId, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*inactive or blocked*");
    }
}
