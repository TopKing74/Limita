using FluentAssertions;
using Moq;
using Moq.EntityFrameworkCore;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Transactions.Models;
using Limita.Application.Features.Transactions.Services;
using Limita.Domain.Entities;
using Limita.Domain.Enums;
using Xunit;

namespace Limita.Application.Tests.Features.Transactions;

public class TransactionServiceTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly TransactionService _transactionService;

    public TransactionServiceTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _transactionService = new TransactionService(_contextMock.Object);
    }

    [Fact]
    public async Task GetTransactionsSummaryAsync_ShouldThrowKeyNotFoundException_WhenAccountIdDoesNotBelongToUser()
    {
        // Arrange
        var userId = "user-1";
        var userAccountId = Guid.NewGuid();
        var foreignAccountId = Guid.NewGuid();

        var accounts = new List<Account>
        {
            new() { Id = userAccountId, UserId = userId, Currency = "EGP" }
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(accounts);

        // Act
        var act = async () => await _transactionService.GetTransactionsSummaryAsync(userId, foreignAccountId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{foreignAccountId}*");
    }

    [Fact]
    public async Task GetTransactionsSummaryAsync_ShouldGroupTotalsByCurrency_WhenUserHasMultiCurrencyAccounts()
    {
        // Arrange
        var userId = "user-1";
        var egpAccountId = Guid.NewGuid();
        var usdAccountId = Guid.NewGuid();

        var accounts = new List<Account>
        {
            new() { Id = egpAccountId, UserId = userId, Currency = "EGP" },
            new() { Id = usdAccountId, UserId = userId, Currency = "USD" }
        };

        var transactions = new List<Transaction>
        {
            // EGP Account transactions
            new()
            {
                AccountId = egpAccountId,
                Currency = "EGP",
                Amount = 10000m,
                Type = TransactionType.Deposit,
                Status = TransactionStatus.Completed
            },
            new()
            {
                AccountId = egpAccountId,
                Currency = "EGP",
                Amount = 2000m,
                Type = TransactionType.BillPayment,
                Status = TransactionStatus.Completed
            },
            // USD Account transactions
            new()
            {
                AccountId = usdAccountId,
                Currency = "USD",
                Amount = 500m,
                Type = TransactionType.Deposit,
                Status = TransactionStatus.Completed
            },
            new()
            {
                AccountId = usdAccountId,
                Currency = "USD",
                Amount = 100m,
                Type = TransactionType.Withdrawal,
                Status = TransactionStatus.Completed
            }
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(accounts);
        _contextMock.Setup(c => c.Transactions).ReturnsDbSet(transactions);

        // Act
        var result = await _transactionService.GetTransactionsSummaryAsync(userId, null);

        // Assert
        result.Should().NotBeNull();
        result.Currency.Should().Be("EGP");

        // Top level must NOT mix EGP and USD!
        result.TotalInflow.Should().Be(10000m);
        result.TotalOutflow.Should().Be(2000m);
        result.NetCashFlow.Should().Be(8000m);
        result.TotalTransactionsCount.Should().Be(2);

        // Currencies breakdown must accurately isolate both currencies
        result.Currencies.Should().NotBeNull();
        result.Currencies.Should().HaveCount(2);

        var egpSummary = result.Currencies!.FirstOrDefault(c => c.Currency == "EGP");
        egpSummary.Should().NotBeNull();
        egpSummary!.TotalInflow.Should().Be(10000m);
        egpSummary.TotalOutflow.Should().Be(2000m);
        egpSummary.NetCashFlow.Should().Be(8000m);
        egpSummary.TotalTransactionsCount.Should().Be(2);

        var usdSummary = result.Currencies!.FirstOrDefault(c => c.Currency == "USD");
        usdSummary.Should().NotBeNull();
        usdSummary!.TotalInflow.Should().Be(500m);
        usdSummary.TotalOutflow.Should().Be(100m);
        usdSummary.NetCashFlow.Should().Be(400m);
        usdSummary.TotalTransactionsCount.Should().Be(2);
    }

    [Fact]
    public async Task GetTransactionsSummaryAsync_ShouldReturnAccountSpecificSummary_WhenValidAccountIdProvided()
    {
        // Arrange
        var userId = "user-1";
        var egpAccountId = Guid.NewGuid();
        var usdAccountId = Guid.NewGuid();

        var accounts = new List<Account>
        {
            new() { Id = egpAccountId, UserId = userId, Currency = "EGP" },
            new() { Id = usdAccountId, UserId = userId, Currency = "USD" }
        };

        var transactions = new List<Transaction>
        {
            new()
            {
                AccountId = egpAccountId,
                Currency = "EGP",
                Amount = 10000m,
                Type = TransactionType.Deposit,
                Status = TransactionStatus.Completed
            },
            new()
            {
                AccountId = usdAccountId,
                Currency = "USD",
                Amount = 750m,
                Type = TransactionType.TransferIn,
                Status = TransactionStatus.Completed
            },
            new()
            {
                AccountId = usdAccountId,
                Currency = "USD",
                Amount = 250m,
                Type = TransactionType.TransferOut,
                Status = TransactionStatus.Completed
            }
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(accounts);
        _contextMock.Setup(c => c.Transactions).ReturnsDbSet(transactions);

        // Act: Filter specifically for usdAccountId
        var result = await _transactionService.GetTransactionsSummaryAsync(userId, usdAccountId);

        // Assert
        result.Should().NotBeNull();
        result.Currency.Should().Be("USD");
        result.TotalInflow.Should().Be(750m);
        result.TotalOutflow.Should().Be(250m);
        result.NetCashFlow.Should().Be(500m);
        result.TotalTransactionsCount.Should().Be(2);

        result.Currencies.Should().HaveCount(1);
        result.Currencies![0].Currency.Should().Be("USD");
        result.Currencies[0].TotalInflow.Should().Be(750m);
        result.Currencies[0].TotalOutflow.Should().Be(250m);
    }

    [Fact]
    public async Task GetTransactionsSummaryAsync_ShouldReturnZeroes_WhenUserHasNoAccounts()
    {
        // Arrange
        var userId = "user-empty";
        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account>());

        // Act
        var result = await _transactionService.GetTransactionsSummaryAsync(userId, null);

        // Assert
        result.Should().NotBeNull();
        result.TotalInflow.Should().Be(0m);
        result.TotalOutflow.Should().Be(0m);
        result.NetCashFlow.Should().Be(0m);
        result.TotalTransactionsCount.Should().Be(0);
        result.Currencies.Should().BeEmpty();
    }
}
