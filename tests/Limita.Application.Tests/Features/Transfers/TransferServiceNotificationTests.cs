using FluentAssertions;
using Moq;
using Moq.EntityFrameworkCore;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Notifications.Services;
using Limita.Application.Features.Transfers.Services;
using Limita.Domain.Entities;
using Limita.Domain.Enums;
using Xunit;

namespace Limita.Application.Tests.Features.Transfers;

public class TransferServiceNotificationTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly TransferService _transferService;

    public TransferServiceNotificationTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _notificationServiceMock = new Mock<INotificationService>();
        _transferService = new TransferService(_contextMock.Object, _notificationServiceMock.Object);
    }

    [Fact]
    public async Task ConfirmTransferAsync_ShouldNotifyBothSenderAndRecipient_WhenTransferIsConfirmedSuccessfully()
    {
        // Arrange
        var senderUserId = "sender-user-1";
        var recipientUserId = "recipient-user-2";
        var transferId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();

        var sourceAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = senderUserId,
            AccountNumber = "1000000001",
            Balance = 1000m,
            Currency = "EGP",
            Status = AccountStatus.Active
        };

        var destinationAccount = new Account
        {
            Id = Guid.NewGuid(),
            UserId = recipientUserId,
            AccountNumber = "2000000002",
            Balance = 200m,
            Currency = "EGP",
            Status = AccountStatus.Active
        };

        var pendingTransaction = new Transaction
        {
            Id = transactionId,
            AccountId = sourceAccount.Id,
            ReferenceNumber = "TRF-TEST-REF",
            Amount = 300m,
            Status = TransactionStatus.Pending
        };

        var transfer = new Transfer
        {
            Id = transferId,
            SourceAccountId = sourceAccount.Id,
            SourceAccount = sourceAccount,
            DestinationAccountId = destinationAccount.Id,
            DestinationAccount = destinationAccount,
            TransactionId = transactionId,
            Transaction = pendingTransaction,
            Amount = 300m,
            Status = TransferStatus.Pending
        };

        _contextMock.Setup(c => c.Transfers).ReturnsDbSet(new List<Transfer> { transfer });
        _contextMock.Setup(c => c.Transactions).ReturnsDbSet(new List<Transaction> { pendingTransaction });

        // Act
        var result = await _transferService.ConfirmTransferAsync(senderUserId, transferId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // 1. Verify debit notification to sender
        _notificationServiceMock.Verify(n => n.CreateAndSendNotificationAsync(
            senderUserId,
            "Transfer Sent",
            It.Is<string>(msg => msg.Contains("300.00") && msg.Contains("2000000002")),
            "Transaction",
            "TRF-TEST-REF",
            It.IsAny<CancellationToken>()), Times.Once);

        // 2. Verify credit notification to recipient
        _notificationServiceMock.Verify(n => n.CreateAndSendNotificationAsync(
            recipientUserId,
            "Transfer Received",
            It.Is<string>(msg => msg.Contains("300.00") && msg.Contains("1000000001")),
            "Transaction",
            It.Is<string>(r => r.EndsWith("-IN")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
