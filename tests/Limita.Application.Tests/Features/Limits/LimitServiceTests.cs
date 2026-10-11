using FluentAssertions;
using Moq;
using Moq.EntityFrameworkCore;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Limits.Models;
using Limita.Application.Features.Limits.Services;
using Limita.Application.Features.Limits.Validators;
using Limita.Application.Features.Transfers.Models;
using Limita.Application.Features.Transfers.Services;
using Limita.Domain.Entities;
using Limita.Domain.Enums;
using Xunit;

namespace Limita.Application.Tests.Features.Limits;

public class LimitServiceTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly LimitService _limitService;

    public LimitServiceTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _limitService = new LimitService(_contextMock.Object);
    }

    [Fact]
    public async Task ValidateDebitLimitAsync_ShouldFail_WhenAmountExceedsPerTransactionLimit()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = new Account
        {
            Id = accountId,
            AccountNumber = "1000100010",
            Currency = "EGP",
            Balance = 100000m,
            PerTransactionLimit = 15000m, // 15K per transaction
            DailyLimit = 50000m,
            MonthlyLimit = 250000m
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { account });
        _contextMock.Setup(c => c.Transactions).ReturnsDbSet(new List<Transaction>());

        // Act - Request 20,000 (exceeds 15,000 single transaction limit)
        var result = await _limitService.ValidateDebitLimitAsync(accountId, 20000m);

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("exceeds the maximum per-transaction limit of 15,000.00 EGP");
    }

    [Fact]
    public async Task ValidateDebitLimitAsync_ShouldFail_WhenAmountExceedsRemainingDailyLimit()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = new Account
        {
            Id = accountId,
            AccountNumber = "1000100010",
            Currency = "EGP",
            Balance = 100000m,
            PerTransactionLimit = 50000m,
            DailyLimit = 50000m, // 50K daily
            MonthlyLimit = 250000m
        };

        // User already spent 40,000 today
        var priorTransactionToday = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            ReferenceNumber = "TRF-PRIOR-1",
            Type = TransactionType.TransferOut,
            Amount = 40000m,
            Status = TransactionStatus.Completed,
            CreatedAt = DateTime.UtcNow.Date.AddHours(2)
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { account });
        _contextMock.Setup(c => c.Transactions).ReturnsDbSet(new List<Transaction> { priorTransactionToday });

        // Act - Try to send 15,000 (40,000 + 15,000 = 55,000 > 50,000 DailyLimit)
        var result = await _limitService.ValidateDebitLimitAsync(accountId, 15000m);

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Daily transfer limit exceeded");
        result.RemainingDailyLimit.Should().Be(10000m);
    }

    [Fact]
    public async Task ValidateDebitLimitAsync_ShouldSucceed_WhenAmountIsWithinAllLimits()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var account = new Account
        {
            Id = accountId,
            AccountNumber = "1000100010",
            Currency = "EGP",
            Balance = 100000m,
            PerTransactionLimit = 25000m,
            DailyLimit = 50000m,
            MonthlyLimit = 250000m
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { account });
        _contextMock.Setup(c => c.Transactions).ReturnsDbSet(new List<Transaction>());

        // Act - 10,000 within all limits
        var result = await _limitService.ValidateDebitLimitAsync(accountId, 10000m);

        // Assert
        result.IsAllowed.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.RemainingDailyLimit.Should().Be(40000m);
    }

    [Fact]
    public async Task GetAccountLimitsAsync_ShouldReturnAccurateMetrics()
    {
        // Arrange
        var userId = "test-user-limits";
        var accountId = Guid.NewGuid();
        var account = new Account
        {
            Id = accountId,
            UserId = userId,
            AccountNumber = "1000100010",
            Currency = "EGP",
            Balance = 100000m,
            PerTransactionLimit = 20000m,
            DailyLimit = 50000m,
            MonthlyLimit = 250000m
        };

        var transactionToday = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            ReferenceNumber = "WTH-1",
            Type = TransactionType.Withdrawal,
            Amount = 15000m,
            Status = TransactionStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { account });
        _contextMock.Setup(c => c.Transactions).ReturnsDbSet(new List<Transaction> { transactionToday });

        // Act
        var response = await _limitService.GetAccountLimitsAsync(userId, accountId);

        // Assert
        response.Success.Should().BeTrue();
        var data = response.Data!;
        data.AccountId.Should().Be(accountId);
        data.PerTransactionLimit.Should().Be(20000m);
        data.DailyLimit.Should().Be(50000m);
        data.SpentToday.Should().Be(15000m);
        data.RemainingDailyLimit.Should().Be(35000m);
    }

    [Fact]
    public void UpdateAccountLimitsRequestValidator_ShouldFail_WhenPerTransactionLimitExceedsDailyLimit()
    {
        // Arrange
        var validator = new UpdateAccountLimitsRequestValidator();
        var request = new UpdateAccountLimitsRequest(
            PerTransactionLimit: 60000m, // 60K per transaction
            DailyLimit: 50000m,          // 50K daily ceiling (violates constraint)
            MonthlyLimit: 250000m);

        // Act
        var validationResult = validator.Validate(request);

        // Assert
        validationResult.IsValid.Should().BeFalse();
        validationResult.Errors.Should().Contain(e => e.ErrorMessage.Contains("Per-transaction limit cannot exceed the daily limit."));
    }

    [Fact]
    public async Task TransferService_CreateTransferAsync_ShouldRejectTransfer_WhenDailyLimitIsExceeded()
    {
        // Arrange
        var senderUserId = "sender-limit-user";
        var sourceAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = senderUserId,
            AccountNumber = "1111111111",
            Balance = 500000m, // Plenty of balance
            Currency = "EGP",
            Status = AccountStatus.Active,
            PerTransactionLimit = 50000m,
            DailyLimit = 50000m, // But 50K daily limit
            MonthlyLimit = 250000m
        };

        var destinationAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = "recipient-user",
            AccountNumber = "2222222222",
            Currency = "EGP",
            Status = AccountStatus.Active
        };

        // Prior transaction of 45,000 today
        var priorTx = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = sourceAccount.Id,
            ReferenceNumber = "PRIOR-TX-1",
            Type = TransactionType.TransferOut,
            Amount = 45000m,
            Status = TransactionStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { sourceAccount, destinationAccount });
        _contextMock.Setup(c => c.Transactions).ReturnsDbSet(new List<Transaction> { priorTx });
        _contextMock.Setup(c => c.Transfers).ReturnsDbSet(new List<Transfer>());

        var transferService = new TransferService(_contextMock.Object, limitService: _limitService);

        var request = new CreateTransferRequest(
            SourceAccountId: sourceAccount.Id,
            DestinationAccountNumber: destinationAccount.AccountNumber,
            Amount: 10000m); // 45,000 + 10,000 = 55,000 > 50,000 limit

        // Act
        var result = await transferService.CreateTransferAsync(senderUserId, request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Daily transfer limit exceeded");
    }

    [Fact]
    public async Task UpdateAccountLimitsAsync_ShouldFail_WhenPartialUpdateCausesMonthlyLimitToBeLessThanExistingDailyLimit()
    {
        // Arrange
        var userId = "test-user-partial-limits";
        var accountId = Guid.NewGuid();
        var account = new Account
        {
            Id = accountId,
            UserId = userId,
            AccountNumber = "1000100010",
            Currency = "EGP",
            Status = AccountStatus.Active,
            PerTransactionLimit = 5000m,
            DailyLimit = 20000m,
            MonthlyLimit = 100000m
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { account });

        // Act: User only updates MonthlyLimit to 15,000 (which is less than existing DailyLimit of 20,000)
        var request = new UpdateAccountLimitsRequest(
            PerTransactionLimit: null,
            DailyLimit: null,
            MonthlyLimit: 15000m);

        var response = await _limitService.UpdateAccountLimitsAsync(userId, accountId, request);

        // Assert
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("Daily limit");
        response.Message.Should().Contain("cannot exceed monthly limit");
        account.MonthlyLimit.Should().Be(100000m); // Unchanged
    }

    [Fact]
    public async Task UpdateAccountLimitsAsync_ShouldFail_WhenPartialUpdateCausesPerTransactionLimitToExceedExistingDailyLimit()
    {
        // Arrange
        var userId = "test-user-partial-limits";
        var accountId = Guid.NewGuid();
        var account = new Account
        {
            Id = accountId,
            UserId = userId,
            AccountNumber = "1000100010",
            Currency = "EGP",
            Status = AccountStatus.Active,
            PerTransactionLimit = 5000m,
            DailyLimit = 20000m,
            MonthlyLimit = 100000m
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { account });

        // Act: User only updates PerTransactionLimit to 25,000 (which exceeds existing DailyLimit of 20,000)
        var request = new UpdateAccountLimitsRequest(
            PerTransactionLimit: 25000m,
            DailyLimit: null,
            MonthlyLimit: null);

        var response = await _limitService.UpdateAccountLimitsAsync(userId, accountId, request);

        // Assert
        response.Success.Should().BeFalse();
        response.Message.Should().Contain("Per-transaction limit");
        response.Message.Should().Contain("cannot exceed daily limit");
        account.PerTransactionLimit.Should().Be(5000m); // Unchanged
    }

    [Fact]
    public async Task UpdateAccountLimitsAsync_ShouldSucceed_WhenPartialUpdateMaintainsValidMergedLimitsOrder()
    {
        // Arrange
        var userId = "test-user-partial-limits";
        var accountId = Guid.NewGuid();
        var account = new Account
        {
            Id = accountId,
            UserId = userId,
            AccountNumber = "1000100010",
            Currency = "EGP",
            Status = AccountStatus.Active,
            PerTransactionLimit = 5000m,
            DailyLimit = 20000m,
            MonthlyLimit = 100000m
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { account });
        _contextMock.Setup(c => c.Transactions).ReturnsDbSet(new List<Transaction>());

        // Act: Partial update of DailyLimit to 30,000 (valid: 5,000 <= 30,000 <= 100,000)
        var request = new UpdateAccountLimitsRequest(
            PerTransactionLimit: null,
            DailyLimit: 30000m,
            MonthlyLimit: null);

        var response = await _limitService.UpdateAccountLimitsAsync(userId, accountId, request);

        // Assert
        response.Success.Should().BeTrue();
        account.PerTransactionLimit.Should().Be(5000m);
        account.DailyLimit.Should().Be(30000m);
        account.MonthlyLimit.Should().Be(100000m);
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
