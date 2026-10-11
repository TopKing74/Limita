using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Moq.EntityFrameworkCore;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Exchange.Models;
using Limita.Application.Features.Exchange.Services;
using Limita.Application.Features.Limits.Models;
using Limita.Application.Features.Limits.Services;
using Limita.Domain.Entities;
using Limita.Domain.Enums;
using Xunit;

namespace Limita.Application.Tests.Features.Exchange;

public class ExchangeServiceTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<ILimitService> _limitServiceMock;
    private readonly ExchangeService _exchangeService;

    public ExchangeServiceTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _limitServiceMock = new Mock<ILimitService>();
        _exchangeService = new ExchangeService(_contextMock.Object, _limitServiceMock.Object);
    }

    [Fact]
    public async Task ExecuteExchangeAsync_ShouldSucceed_WhenValidAccountsAndWithinLimits()
    {
        // Arrange (Test 1: Successful exchange between two different currencies verifying source debit, destination credit, and ledger entries)
        var userId = "user-test-fx";
        var sourceAccountId = Guid.NewGuid();
        var destinationAccountId = Guid.NewGuid();

        var sourceAccount = new Account
        {
            Id = sourceAccountId,
            UserId = userId,
            AccountNumber = "1111000001",
            Balance = 1000m,
            Currency = "USD",
            Status = AccountStatus.Active
        };

        var destinationAccount = new Account
        {
            Id = destinationAccountId,
            UserId = userId,
            AccountNumber = "2222000002",
            Balance = 5000m,
            Currency = "EGP",
            Status = AccountStatus.Active
        };

        var rate = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            FromCurrency = "USD",
            ToCurrency = "EGP",
            Rate = 50.0m,
            UpdatedAt = DateTime.UtcNow
        };

        var addedTransactions = new List<Transaction>();

        _contextMock.Setup(c => c.Accounts)
            .ReturnsDbSet(new List<Account> { sourceAccount, destinationAccount });
        _contextMock.Setup(c => c.ExchangeRates)
            .ReturnsDbSet(new List<ExchangeRate> { rate });
        _contextMock.Setup(c => c.Transactions)
            .ReturnsDbSet(addedTransactions);

        _contextMock.Setup(c => c.Transactions.Add(It.IsAny<Transaction>()))
            .Callback<Transaction>(t => addedTransactions.Add(t));

        _limitServiceMock.Setup(l => l.ValidateDebitLimitAsync(sourceAccountId, 100m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResult(IsAllowed: true));

        var request = new ExecuteExchangeRequest(
            SourceAccountId: sourceAccountId,
            DestinationAccountId: destinationAccountId,
            Amount: 100m);

        // Act
        var result = await _exchangeService.ExecuteExchangeAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();

        // 1. Verify debited and credited amounts in DTO
        result.Data!.DebitedAmount.Should().Be(100m);
        result.Data.CreditedAmount.Should().Be(5000m); // 100 * 50 = 5000
        result.Data.AppliedRate.Should().Be(50.0m);
        result.Data.FromCurrency.Should().Be("USD");
        result.Data.ToCurrency.Should().Be("EGP");

        // 2. Verify account balance mutations
        sourceAccount.Balance.Should().Be(900m);
        destinationAccount.Balance.Should().Be(10000m);

        // 3. Verify ledger entries
        addedTransactions.Should().HaveCount(2);

        var debitTx = addedTransactions.FirstOrDefault(t => t.Type == TransactionType.ExchangeOut);
        debitTx.Should().NotBeNull();
        debitTx!.AccountId.Should().Be(sourceAccountId);
        debitTx.Amount.Should().Be(100m);
        debitTx.Currency.Should().Be("USD");
        debitTx.Status.Should().Be(TransactionStatus.Completed);
        debitTx.ReferenceNumber.Should().EndWith("-OUT");

        var creditTx = addedTransactions.FirstOrDefault(t => t.Type == TransactionType.ExchangeIn);
        creditTx.Should().NotBeNull();
        creditTx!.AccountId.Should().Be(destinationAccountId);
        creditTx.Amount.Should().Be(5000m);
        creditTx.Currency.Should().Be("EGP");
        creditTx.Status.Should().Be(TransactionStatus.Completed);
        creditTx.ReferenceNumber.Should().EndWith("-IN");

        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteExchangeAsync_ShouldFail_WhenDebitExceedsLimit()
    {
        // Arrange (Test 2: Validation failure when debit exceeds PerTransactionLimit or DailyLimit)
        var userId = "user-test-fx";
        var sourceAccountId = Guid.NewGuid();
        var destinationAccountId = Guid.NewGuid();

        var sourceAccount = new Account
        {
            Id = sourceAccountId,
            UserId = userId,
            AccountNumber = "1111000001",
            Balance = 50000m,
            Currency = "USD",
            Status = AccountStatus.Active
        };

        var destinationAccount = new Account
        {
            Id = destinationAccountId,
            UserId = userId,
            AccountNumber = "2222000002",
            Balance = 1000m,
            Currency = "EGP",
            Status = AccountStatus.Active
        };

        var rate = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            FromCurrency = "USD",
            ToCurrency = "EGP",
            Rate = 50.0m
        };

        _contextMock.Setup(c => c.Accounts)
            .ReturnsDbSet(new List<Account> { sourceAccount, destinationAccount });
        _contextMock.Setup(c => c.ExchangeRates)
            .ReturnsDbSet(new List<ExchangeRate> { rate });
        _contextMock.Setup(c => c.Transactions)
            .ReturnsDbSet(new List<Transaction>());

        // Mock ILimitService returning failure
        _limitServiceMock.Setup(l => l.ValidateDebitLimitAsync(sourceAccountId, 10000m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResult(
                IsAllowed: false,
                ErrorMessage: "Transaction amount (10,000.00 USD) exceeds the maximum per-transaction limit of 5,000.00 USD."));

        var request = new ExecuteExchangeRequest(
            SourceAccountId: sourceAccountId,
            DestinationAccountId: destinationAccountId,
            Amount: 10000m);

        // Act
        var result = await _exchangeService.ExecuteExchangeAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("exceeds the maximum per-transaction limit");

        // Verify balances were not touched and no database changes occurred
        sourceAccount.Balance.Should().Be(50000m);
        destinationAccount.Balance.Should().Be(1000m);
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteExchangeAsync_ShouldFail_WhenSourceBalanceIsInsufficient()
    {
        // Arrange (Test 3: Rejection on insufficient balance)
        var userId = "user-test-fx";
        var sourceAccountId = Guid.NewGuid();
        var destinationAccountId = Guid.NewGuid();

        var sourceAccount = new Account
        {
            Id = sourceAccountId,
            UserId = userId,
            AccountNumber = "1111000001",
            Balance = 50m, // Only 50 USD available
            Currency = "USD",
            Status = AccountStatus.Active
        };

        var destinationAccount = new Account
        {
            Id = destinationAccountId,
            UserId = userId,
            AccountNumber = "2222000002",
            Balance = 1000m,
            Currency = "EGP",
            Status = AccountStatus.Active
        };

        var rate = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            FromCurrency = "USD",
            ToCurrency = "EGP",
            Rate = 50.0m
        };

        _contextMock.Setup(c => c.Accounts)
            .ReturnsDbSet(new List<Account> { sourceAccount, destinationAccount });
        _contextMock.Setup(c => c.ExchangeRates)
            .ReturnsDbSet(new List<ExchangeRate> { rate });
        _contextMock.Setup(c => c.Transactions)
            .ReturnsDbSet(new List<Transaction>());

        _limitServiceMock.Setup(l => l.ValidateDebitLimitAsync(sourceAccountId, 100m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResult(IsAllowed: true));

        var request = new ExecuteExchangeRequest(
            SourceAccountId: sourceAccountId,
            DestinationAccountId: destinationAccountId,
            Amount: 100m); // Request 100 USD

        // Act
        var result = await _exchangeService.ExecuteExchangeAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.ToLower().Should().Contain("insufficient balance");

        sourceAccount.Balance.Should().Be(50m);
        destinationAccount.Balance.Should().Be(1000m);
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("different-user", AccountStatus.Active, AccountStatus.Active, "does not belong to you")]
    [InlineData("user-test-fx", AccountStatus.Suspended, AccountStatus.Active, "cannot perform exchanges")]
    [InlineData("user-test-fx", AccountStatus.Active, AccountStatus.Frozen, "cannot receive exchanges")]
    public async Task ExecuteExchangeAsync_ShouldFail_WhenAccountsBelongToDifferentUserOrAreInactive(
        string destinationOwnerId,
        AccountStatus sourceStatus,
        AccountStatus destinationStatus,
        string expectedErrorSnippet)
    {
        // Arrange (Test 4: Rejection when accounts belong to different users or are inactive)
        var userId = "user-test-fx";
        var sourceAccountId = Guid.NewGuid();
        var destinationAccountId = Guid.NewGuid();

        var sourceAccount = new Account
        {
            Id = sourceAccountId,
            UserId = userId,
            AccountNumber = "1111000001",
            Balance = 1000m,
            Currency = "USD",
            Status = sourceStatus
        };

        var destinationAccount = new Account
        {
            Id = destinationAccountId,
            UserId = destinationOwnerId,
            AccountNumber = "2222000002",
            Balance = 5000m,
            Currency = "EGP",
            Status = destinationStatus
        };

        var rate = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            FromCurrency = "USD",
            ToCurrency = "EGP",
            Rate = 50.0m
        };

        _contextMock.Setup(c => c.Accounts)
            .ReturnsDbSet(new List<Account> { sourceAccount, destinationAccount });
        _contextMock.Setup(c => c.ExchangeRates)
            .ReturnsDbSet(new List<ExchangeRate> { rate });
        _contextMock.Setup(c => c.Transactions)
            .ReturnsDbSet(new List<Transaction>());

        _limitServiceMock.Setup(l => l.ValidateDebitLimitAsync(sourceAccountId, 100m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResult(IsAllowed: true));

        var request = new ExecuteExchangeRequest(
            SourceAccountId: sourceAccountId,
            DestinationAccountId: destinationAccountId,
            Amount: 100m);

        // Act
        var result = await _exchangeService.ExecuteExchangeAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain(expectedErrorSnippet);
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteExchangeAsync_ShouldRollbackTransaction_OnDatabaseFailure()
    {
        // Arrange (Test 5: Rollback verification on database failure)
        var userId = "user-test-fx";
        var sourceAccountId = Guid.NewGuid();
        var destinationAccountId = Guid.NewGuid();

        var sourceAccount = new Account
        {
            Id = sourceAccountId,
            UserId = userId,
            AccountNumber = "1111000001",
            Balance = 1000m,
            Currency = "USD",
            Status = AccountStatus.Active
        };

        var destinationAccount = new Account
        {
            Id = destinationAccountId,
            UserId = userId,
            AccountNumber = "2222000002",
            Balance = 5000m,
            Currency = "EGP",
            Status = AccountStatus.Active
        };

        var rate = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            FromCurrency = "USD",
            ToCurrency = "EGP",
            Rate = 50.0m
        };

        _contextMock.Setup(c => c.Accounts)
            .ReturnsDbSet(new List<Account> { sourceAccount, destinationAccount });
        _contextMock.Setup(c => c.ExchangeRates)
            .ReturnsDbSet(new List<ExchangeRate> { rate });
        _contextMock.Setup(c => c.Transactions)
            .ReturnsDbSet(new List<Transaction>());

        _limitServiceMock.Setup(l => l.ValidateDebitLimitAsync(sourceAccountId, 100m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResult(IsAllowed: true));

        var transactionMock = new Mock<IDbContextTransaction>();
        _contextMock.Setup(c => c.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactionMock.Object);

        _contextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Simulated transient database connection failure", new Exception()));

        var request = new ExecuteExchangeRequest(
            SourceAccountId: sourceAccountId,
            DestinationAccountId: destinationAccountId,
            Amount: 100m);

        // Act
        var act = async () => await _exchangeService.ExecuteExchangeAsync(userId, request);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>()
            .WithMessage("Simulated transient database connection failure");

        // Verify transaction rollback was invoked
        transactionMock.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        transactionMock.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
