using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.EntityFrameworkCore;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Limits.Models;
using Limita.Application.Features.Limits.Services;
using Limita.Application.Features.Notifications.Services;
using Limita.Application.Features.Transfers.Models;
using Limita.Application.Features.Transfers.Services;
using Limita.Domain.Entities;
using Limita.Domain.Enums;
using Xunit;

namespace Limita.Application.Tests.Features.Transfers;

public class TransferServiceTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<ILimitService> _limitServiceMock;
    private readonly Mock<ILogger<TransferService>> _loggerMock;
    private readonly TransferService _transferService;

    public TransferServiceTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _notificationServiceMock = new Mock<INotificationService>();
        _limitServiceMock = new Mock<ILimitService>();
        _loggerMock = new Mock<ILogger<TransferService>>();

        _transferService = new TransferService(
            _contextMock.Object,
            _notificationServiceMock.Object,
            _loggerMock.Object,
            _limitServiceMock.Object);
    }

    [Fact]
    public async Task CreateTransferAsync_ShouldFail_WhenSourceAccountNotFoundOrNotOwned()
    {
        // Arrange
        var userId = "user-1";
        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account>());

        var request = new CreateTransferRequest(Guid.NewGuid(), "2000000002", 500m, "Payment");

        // Act
        var result = await _transferService.CreateTransferAsync(userId, request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Source account was not found or does not belong to you");
    }

    [Fact]
    public async Task CreateTransferAsync_ShouldFail_WhenSourceAccountIsInactive()
    {
        // Arrange
        var userId = "user-1";
        var sourceAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = "1000000001",
            Balance = 5000m,
            Currency = "EGP",
            Status = AccountStatus.Suspended
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { sourceAccount });

        var request = new CreateTransferRequest(sourceAccount.Id, "2000000002", 500m, "Payment");

        // Act
        var result = await _transferService.CreateTransferAsync(userId, request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("suspended");
    }

    [Fact]
    public async Task CreateTransferAsync_ShouldFail_WhenDestinationAccountNotFound()
    {
        // Arrange
        var userId = "user-1";
        var sourceAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = "1000000001",
            Balance = 5000m,
            Currency = "EGP",
            Status = AccountStatus.Active
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { sourceAccount });

        var request = new CreateTransferRequest(sourceAccount.Id, "NON_EXISTENT_ACC", 500m, "Payment");

        // Act
        var result = await _transferService.CreateTransferAsync(userId, request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("was not found");
    }

    [Fact]
    public async Task CreateTransferAsync_ShouldFail_WhenAttemptingSelfTransfer()
    {
        // Arrange
        var userId = "user-1";
        var accountId = Guid.NewGuid();
        var account = new Account
        {
            Id = accountId,
            UserId = userId,
            AccountNumber = "1000000001",
            Balance = 5000m,
            Currency = "EGP",
            Status = AccountStatus.Active
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { account });

        var request = new CreateTransferRequest(accountId, "1000000001", 500m, "Self transfer");

        // Act
        var result = await _transferService.CreateTransferAsync(userId, request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Cannot transfer funds to the same source account");
    }

    [Fact]
    public async Task CreateTransferAsync_ShouldFail_WhenCurrencyMismatch()
    {
        // Arrange
        var userId = "user-1";
        var sourceAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = "1000000001",
            Balance = 5000m,
            Currency = "EGP",
            Status = AccountStatus.Active
        };

        var destAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = "user-2",
            AccountNumber = "2000000002",
            Balance = 100m,
            Currency = "USD", // Mismatch
            Status = AccountStatus.Active
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { sourceAccount, destAccount });

        var request = new CreateTransferRequest(sourceAccount.Id, "2000000002", 500m, "Cross-currency transfer");

        // Act
        var result = await _transferService.CreateTransferAsync(userId, request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Currency mismatch");
    }

    [Fact]
    public async Task CreateTransferAsync_ShouldFail_WhenInsufficientBalance()
    {
        // Arrange
        var userId = "user-1";
        var sourceAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = "1000000001",
            Balance = 200m, // Balance is 200
            Currency = "EGP",
            Status = AccountStatus.Active
        };

        var destAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = "user-2",
            AccountNumber = "2000000002",
            Balance = 500m,
            Currency = "EGP",
            Status = AccountStatus.Active
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { sourceAccount, destAccount });

        var request = new CreateTransferRequest(sourceAccount.Id, "2000000002", 1000m, "Big transfer"); // Request 1000

        // Act
        var result = await _transferService.CreateTransferAsync(userId, request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Insufficient balance");
    }

    [Fact]
    public async Task CreateTransferAsync_ShouldFail_WhenLimitCheckFails()
    {
        // Arrange
        var userId = "user-1";
        var sourceAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = "1000000001",
            Balance = 50000m,
            Currency = "EGP",
            Status = AccountStatus.Active
        };

        var destAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = "user-2",
            AccountNumber = "2000000002",
            Balance = 500m,
            Currency = "EGP",
            Status = AccountStatus.Active
        };

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { sourceAccount, destAccount });

        _limitServiceMock.Setup(l => l.ValidateDebitLimitAsync(sourceAccount.Id, 25000m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResult(IsAllowed: false, ErrorMessage: "Daily limit of 20,000 exceeded."));

        var request = new CreateTransferRequest(sourceAccount.Id, "2000000002", 25000m, "Over limit");

        // Act
        var result = await _transferService.CreateTransferAsync(userId, request);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Daily limit of 20,000 exceeded");
    }

    [Fact]
    public async Task CreateTransferAsync_ShouldSucceed_AndCreatePendingTransferAndTransaction()
    {
        // Arrange
        var userId = "user-1";
        var sourceAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = "1000000001",
            Balance = 5000m,
            Currency = "EGP",
            Status = AccountStatus.Active,
            User = new ApplicationUser { FullName = "Sender User" }
        };

        var destAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = "user-2",
            AccountNumber = "2000000002",
            Balance = 500m,
            Currency = "EGP",
            Status = AccountStatus.Active,
            User = new ApplicationUser { FullName = "Recipient User" }
        };

        var transactions = new List<Transaction>();
        var transfers = new List<Transfer>();

        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { sourceAccount, destAccount });
        _contextMock.Setup(c => c.Transactions).ReturnsDbSet(transactions);
        _contextMock.Setup(c => c.Transactions.Add(It.IsAny<Transaction>())).Callback<Transaction>(t => transactions.Add(t));
        _contextMock.Setup(c => c.Transfers).ReturnsDbSet(transfers);
        _contextMock.Setup(c => c.Transfers.Add(It.IsAny<Transfer>())).Callback<Transfer>(tr => transfers.Add(tr));

        _limitServiceMock.Setup(l => l.ValidateDebitLimitAsync(sourceAccount.Id, 1000m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResult(IsAllowed: true));

        var request = new CreateTransferRequest(sourceAccount.Id, "2000000002", 1000m, "Rent Payment");

        // Act
        var result = await _transferService.CreateTransferAsync(userId, request);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Amount.Should().Be(1000m);
        result.Data.Status.Should().Be(TransferStatus.Pending);
        result.Data.DestinationAccountHolderName.Should().Be("Recipient User");
        result.Data.ReferenceNumber.Should().StartWith("TRF-");

        transfers.Should().ContainSingle();
        transfers[0].Status.Should().Be(TransferStatus.Pending);

        transactions.Should().ContainSingle();
        transactions[0].Status.Should().Be(TransactionStatus.Pending);
        transactions[0].Amount.Should().Be(1000m);

        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmTransferAsync_ShouldFail_WhenTransferAlreadyCompleted()
    {
        // Arrange
        var userId = "user-1";
        var transferId = Guid.NewGuid();
        var sourceAccount = new Account { Id = Guid.NewGuid(), UserId = userId, Balance = 5000m, Currency = "EGP", Status = AccountStatus.Active };
        var destAccount = new Account { Id = Guid.NewGuid(), UserId = "user-2", Balance = 500m, Currency = "EGP", Status = AccountStatus.Active };

        var transfer = new Transfer
        {
            Id = transferId,
            SourceAccountId = sourceAccount.Id,
            SourceAccount = sourceAccount,
            DestinationAccountId = destAccount.Id,
            DestinationAccount = destAccount,
            Amount = 500m,
            Status = TransferStatus.Completed // Already completed
        };

        _contextMock.Setup(c => c.Transfers).ReturnsDbSet(new List<Transfer> { transfer });

        // Act
        var result = await _transferService.ConfirmTransferAsync(userId, transferId);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already been confirmed");
    }

    [Fact]
    public async Task ConfirmTransferAsync_ShouldFail_WhenTransferIsCancelled()
    {
        // Arrange
        var userId = "user-1";
        var transferId = Guid.NewGuid();
        var sourceAccount = new Account { Id = Guid.NewGuid(), UserId = userId, Balance = 5000m, Currency = "EGP", Status = AccountStatus.Active };
        var destAccount = new Account { Id = Guid.NewGuid(), UserId = "user-2", Balance = 500m, Currency = "EGP", Status = AccountStatus.Active };

        var transfer = new Transfer
        {
            Id = transferId,
            SourceAccountId = sourceAccount.Id,
            SourceAccount = sourceAccount,
            DestinationAccountId = destAccount.Id,
            DestinationAccount = destAccount,
            Amount = 500m,
            Status = TransferStatus.Cancelled
        };

        _contextMock.Setup(c => c.Transfers).ReturnsDbSet(new List<Transfer> { transfer });

        // Act
        var result = await _transferService.ConfirmTransferAsync(userId, transferId);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Cancelled");
    }

    [Fact]
    public async Task ConfirmTransferAsync_ShouldDebitSourceAndCreditDestination_WhenSuccessful()
    {
        // Arrange
        var userId = "user-1";
        var transferId = Guid.NewGuid();
        var txId = Guid.NewGuid();

        var sourceAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountNumber = "1000000001",
            Balance = 3000m,
            Currency = "EGP",
            Status = AccountStatus.Active,
            User = new ApplicationUser { FullName = "Sender Name" }
        };

        var destAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = "user-2",
            AccountNumber = "2000000002",
            Balance = 1000m,
            Currency = "EGP",
            Status = AccountStatus.Active,
            User = new ApplicationUser { FullName = "Recipient Name" }
        };

        var transaction = new Transaction
        {
            Id = txId,
            AccountId = sourceAccount.Id,
            ReferenceNumber = "TRF-TEST-REF",
            Amount = 500m,
            Currency = "EGP",
            Status = TransactionStatus.Pending
        };

        var transfer = new Transfer
        {
            Id = transferId,
            SourceAccountId = sourceAccount.Id,
            SourceAccount = sourceAccount,
            DestinationAccountId = destAccount.Id,
            DestinationAccount = destAccount,
            TransactionId = txId,
            Transaction = transaction,
            Amount = 500m,
            Status = TransferStatus.Pending
        };

        var transactions = new List<Transaction> { transaction };

        _contextMock.Setup(c => c.Transfers).ReturnsDbSet(new List<Transfer> { transfer });
        _contextMock.Setup(c => c.Transactions).ReturnsDbSet(transactions);
        _contextMock.Setup(c => c.Transactions.Add(It.IsAny<Transaction>())).Callback<Transaction>(t => transactions.Add(t));

        _limitServiceMock.Setup(l => l.ValidateDebitLimitAsync(sourceAccount.Id, 500m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResult(IsAllowed: true));

        // Act
        var result = await _transferService.ConfirmTransferAsync(userId, transferId);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Status.Should().Be(TransferStatus.Completed);
        result.Data.RemainingBalance.Should().Be(2500m);

        sourceAccount.Balance.Should().Be(2500m); // 3000 - 500
        destAccount.Balance.Should().Be(1500m);   // 1000 + 500
        transfer.Status.Should().Be(TransferStatus.Completed);
        transaction.Status.Should().Be(TransactionStatus.Completed);

        // Recipient transaction created
        transactions.Should().Contain(t => t.AccountId == destAccount.Id && t.Type == TransactionType.TransferIn && t.Amount == 500m);
    }

    [Fact]
    public async Task CancelTransferAsync_ShouldUpdateStatusToCancelledAndTransactionToFailed_WhenPending()
    {
        // Arrange
        var userId = "user-1";
        var transferId = Guid.NewGuid();
        var sourceAccount = new Account { Id = Guid.NewGuid(), UserId = userId, Balance = 5000m, Currency = "EGP" };
        var transaction = new Transaction { Id = Guid.NewGuid(), Status = TransactionStatus.Pending, ReferenceNumber = "TRF-CANCEL-1" };

        var transfer = new Transfer
        {
            Id = transferId,
            SourceAccountId = sourceAccount.Id,
            SourceAccount = sourceAccount,
            Transaction = transaction,
            Status = TransferStatus.Pending
        };

        _contextMock.Setup(c => c.Transfers).ReturnsDbSet(new List<Transfer> { transfer });

        // Act
        var result = await _transferService.CancelTransferAsync(userId, transferId);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Status.Should().Be(TransferStatus.Cancelled);

        transfer.Status.Should().Be(TransferStatus.Cancelled);
        transaction.Status.Should().Be(TransactionStatus.Failed);
        transaction.Description.Should().ContainEquivalentOf("cancelled by user");

        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelTransferAsync_ShouldFail_WhenTransferAlreadyCompleted()
    {
        // Arrange
        var userId = "user-1";
        var transferId = Guid.NewGuid();
        var sourceAccount = new Account { Id = Guid.NewGuid(), UserId = userId };

        var transfer = new Transfer
        {
            Id = transferId,
            SourceAccountId = sourceAccount.Id,
            SourceAccount = sourceAccount,
            Status = TransferStatus.Completed
        };

        _contextMock.Setup(c => c.Transfers).ReturnsDbSet(new List<Transfer> { transfer });

        // Act
        var result = await _transferService.CancelTransferAsync(userId, transferId);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Cannot cancel a completed transfer");
    }

    [Fact]
    public async Task GetTransferByIdAsync_ShouldReturnFail_WhenUserIsNotSenderNorRecipient()
    {
        // Arrange
        var transferId = Guid.NewGuid();
        var sourceAccount = new Account { Id = Guid.NewGuid(), UserId = "user-sender" };
        var destAccount = new Account { Id = Guid.NewGuid(), UserId = "user-recipient" };

        var transfer = new Transfer
        {
            Id = transferId,
            SourceAccount = sourceAccount,
            DestinationAccount = destAccount
        };

        _contextMock.Setup(c => c.Transfers).ReturnsDbSet(new List<Transfer> { transfer });

        // Act
        var result = await _transferService.GetTransferByIdAsync("unauthorized-user", transferId);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not authorized");
    }

    [Fact]
    public async Task GetTransferByIdAsync_ShouldSucceed_WhenUserIsSender()
    {
        // Arrange
        var userId = "user-sender";
        var transferId = Guid.NewGuid();
        var sourceAccount = new Account { Id = Guid.NewGuid(), UserId = userId, AccountNumber = "SRC111", Currency = "EGP" };
        var destAccount = new Account { Id = Guid.NewGuid(), UserId = "user-recipient", AccountNumber = "DST222", User = new ApplicationUser { FullName = "Recipient" } };
        var transaction = new Transaction { ReferenceNumber = "TRF-999" };

        var transfer = new Transfer
        {
            Id = transferId,
            SourceAccount = sourceAccount,
            DestinationAccount = destAccount,
            Transaction = transaction,
            Amount = 750m,
            Status = TransferStatus.Pending
        };

        _contextMock.Setup(c => c.Transfers).ReturnsDbSet(new List<Transfer> { transfer });

        // Act
        var result = await _transferService.GetTransferByIdAsync(userId, transferId);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Amount.Should().Be(750m);
        result.Data.SourceAccountNumber.Should().Be("SRC111");
        result.Data.DestinationAccountNumber.Should().Be("DST222");
    }
}
