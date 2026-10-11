using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Moq.EntityFrameworkCore;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Bills.Models;
using Limita.Application.Features.Bills.Services;
using Limita.Domain.Entities;
using Limita.Domain.Enums;
using Xunit;

namespace Limita.Application.Tests.Features.Bills.Services;

public class BillServiceTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly BillService _billService;

    public BillServiceTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _billService = new BillService(_contextMock.Object);
    }

    [Fact]
    public async Task CreateBillAsync_ShouldAddBillAndReturnDto_WhenProviderIsActive()
    {
        // Arrange
        var userId = "user-1";
        var providerId = Guid.NewGuid();
        var provider = new BillProvider
        {
            Id = providerId,
            Name = "Water Co",
            IsActive = true
        };

        var dbProviders = new List<BillProvider> { provider };
        _contextMock.Setup(c => c.BillProviders).ReturnsDbSet(dbProviders);

        var dbBills = new List<Bill>();
        _contextMock.Setup(c => c.Bills).ReturnsDbSet(dbBills);

        var request = new CreateBillRequest(providerId, "12345", 50m, DateTime.UtcNow.AddDays(10));

        // Act
        var result = await _billService.CreateBillAsync(userId, request);

        // Assert
        _contextMock.Verify(c => c.Bills.Add(It.Is<Bill>(b => 
            b.UserId == userId &&
            b.ProviderId == providerId &&
            b.BillingNumber == "12345" &&
            b.Amount == 50m &&
            b.Status == BillStatus.Pending
        )), Times.Once);

        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        result.Should().NotBeNull();
        result.ProviderName.Should().Be("Water Co");
        result.Amount.Should().Be(50m);
        result.BillingNumber.Should().Be("12345");
    }

    [Fact]
    public async Task CreateBillAsync_ShouldThrowInvalidOperationException_WhenProviderNotFound()
    {
        // Arrange
        var dbProviders = new List<BillProvider>();
        _contextMock.Setup(c => c.BillProviders).ReturnsDbSet(dbProviders);

        var request = new CreateBillRequest(Guid.NewGuid(), "12345", 50m, DateTime.UtcNow.AddDays(10));

        // Act
        var action = async () => await _billService.CreateBillAsync("user-1", request);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The specified Bill Provider does not exist.");
    }

    [Fact]
    public async Task CreateBillAsync_ShouldThrowInvalidOperationException_WhenProviderIsInactive()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var provider = new BillProvider
        {
            Id = providerId,
            Name = "Water Co",
            IsActive = false
        };

        var dbProviders = new List<BillProvider> { provider };
        _contextMock.Setup(c => c.BillProviders).ReturnsDbSet(dbProviders);

        var request = new CreateBillRequest(providerId, "12345", 50m, DateTime.UtcNow.AddDays(10));

        // Act
        var action = async () => await _billService.CreateBillAsync("user-1", request);

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("The specified Bill Provider is currently inactive.");
    }

    [Fact]
    public async Task GetUserBillsAsync_ShouldReturnBills_WhenUserHasBills()
    {
        // Arrange
        var userId = "user-1";
        var provider = new BillProvider { Id = Guid.NewGuid(), Name = "Elec Co" };
        var bill1 = new Bill { Id = Guid.NewGuid(), UserId = userId, ProviderId = provider.Id, Provider = provider, Amount = 100m, CreatedAt = DateTime.UtcNow };
        var bill2 = new Bill { Id = Guid.NewGuid(), UserId = userId, ProviderId = provider.Id, Provider = provider, Amount = 200m, CreatedAt = DateTime.UtcNow.AddMinutes(1) };
        var otherUserBill = new Bill { Id = Guid.NewGuid(), UserId = "user-2", ProviderId = provider.Id, Provider = provider, Amount = 300m };

        var dbBills = new List<Bill> { bill1, bill2, otherUserBill };
        _contextMock.Setup(c => c.Bills).ReturnsDbSet(dbBills);

        // Act
        var result = await _billService.GetUserBillsAsync(userId);

        // Assert
        result.Should().HaveCount(2);
        // Sorted by CreatedAt descending
        result.First().Amount.Should().Be(200m); 
        result.Last().Amount.Should().Be(100m);
    }

    [Fact]
    public async Task PayBillAsync_ShouldPayBillAndCreateTransaction_WhenRequestIsValid()
    {
        var userId = "user-1";
        var billId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var bill = new Bill { Id = billId, UserId = userId, Amount = 100m, Status = BillStatus.Pending, BillingNumber = "B-1" };
        var account = new Account { Id = accountId, UserId = userId, Balance = 500m, Status = AccountStatus.Active, Currency = "EGP" };

        _contextMock.Setup(c => c.Bills).ReturnsDbSet(new List<Bill> { bill });
        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { account });
        _contextMock.Setup(c => c.Transactions).ReturnsDbSet(new List<Transaction>());

        var request = new PayBillRequest(accountId);

        var result = await _billService.PayBillAsync(userId, billId, request);

        account.Balance.Should().Be(400m);
        bill.Status.Should().Be(BillStatus.Paid);

        _contextMock.Verify(c => c.Transactions.AddAsync(It.Is<Transaction>(t => 
            t.AccountId == accountId && t.Amount == 100m && t.Type == TransactionType.BillPayment), It.IsAny<CancellationToken>()), Times.Once);
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        result.Should().NotBeNull();
        result.AmountPaid.Should().Be(100m);
        result.BillId.Should().Be(billId);
    }

    [Fact]
    public async Task PayBillAsync_ShouldThrowKeyNotFoundException_WhenBillNotFound()
    {
        _contextMock.Setup(c => c.Bills).ReturnsDbSet(new List<Bill>());

        var action = async () => await _billService.PayBillAsync("user-1", Guid.NewGuid(), new PayBillRequest(Guid.NewGuid()));

        await action.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*Bill not found*");
    }

    [Fact]
    public async Task PayBillAsync_ShouldThrowInvalidOperationException_WhenBillAlreadyPaid()
    {
        var bill = new Bill { Id = Guid.NewGuid(), UserId = "user-1", Status = BillStatus.Paid };
        _contextMock.Setup(c => c.Bills).ReturnsDbSet(new List<Bill> { bill });

        var action = async () => await _billService.PayBillAsync("user-1", bill.Id, new PayBillRequest(Guid.NewGuid()));

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already paid*");
    }

    [Fact]
    public async Task PayBillAsync_ShouldThrowKeyNotFoundException_WhenAccountNotFound()
    {
        var bill = new Bill { Id = Guid.NewGuid(), UserId = "user-1", Status = BillStatus.Pending };
        _contextMock.Setup(c => c.Bills).ReturnsDbSet(new List<Bill> { bill });
        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account>());

        var action = async () => await _billService.PayBillAsync("user-1", bill.Id, new PayBillRequest(Guid.NewGuid()));

        await action.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*Account not found*");
    }

    [Fact]
    public async Task PayBillAsync_ShouldThrowInvalidOperationException_WhenAccountInactive()
    {
        var accountId = Guid.NewGuid();
        var bill = new Bill { Id = Guid.NewGuid(), UserId = "user-1", Status = BillStatus.Pending };
        var account = new Account { Id = accountId, UserId = "user-1", Status = AccountStatus.Frozen };
        
        _contextMock.Setup(c => c.Bills).ReturnsDbSet(new List<Bill> { bill });
        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { account });

        var action = async () => await _billService.PayBillAsync("user-1", bill.Id, new PayBillRequest(accountId));

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*inactive or blocked*");
    }

    [Fact]
    public async Task PayBillAsync_ShouldThrowInvalidOperationException_WhenInsufficientBalance()
    {
        var accountId = Guid.NewGuid();
        var bill = new Bill { Id = Guid.NewGuid(), UserId = "user-1", Amount = 1000m, Status = BillStatus.Pending };
        var account = new Account { Id = accountId, UserId = "user-1", Balance = 500m, Status = AccountStatus.Active };
        
        _contextMock.Setup(c => c.Bills).ReturnsDbSet(new List<Bill> { bill });
        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { account });

        var action = async () => await _billService.PayBillAsync("user-1", bill.Id, new PayBillRequest(accountId));

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Insufficient*");
    }

    [Fact]
    public async Task PayBillAsync_ShouldPreventDoublePayment_AndAtomicallyDebitAndMarkPaid()
    {
        // Arrange
        var userId = "user-1";
        var accountId = Guid.NewGuid();
        var billId = Guid.NewGuid();
        var bill = new Bill
        {
            Id = billId,
            UserId = userId,
            BillingNumber = "BILL-DOUBLE-CHECK",
            Amount = 250m,
            Status = BillStatus.Pending
        };
        var account = new Account
        {
            Id = accountId,
            UserId = userId,
            Balance = 1000m,
            Currency = "EGP",
            Status = AccountStatus.Active
        };

        var dbBills = new List<Bill> { bill };
        var dbAccounts = new List<Account> { account };
        var dbTransactions = new List<Transaction>();

        _contextMock.Setup(c => c.Bills).ReturnsDbSet(dbBills);
        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(dbAccounts);
        _contextMock.Setup(c => c.Transactions).ReturnsDbSet(dbTransactions);

        // Act 1: First payment attempt succeeds
        var firstResponse = await _billService.PayBillAsync(userId, billId, new PayBillRequest(accountId));

        // Assert 1: Account debited, bill marked Paid
        firstResponse.Should().NotBeNull();
        firstResponse.AmountPaid.Should().Be(250m);
        account.Balance.Should().Be(750m);
        bill.Status.Should().Be(BillStatus.Paid);

        // Act 2: Second payment attempt for the now-paid bill
        var doublePayAction = async () => await _billService.PayBillAsync(userId, billId, new PayBillRequest(accountId));

        // Assert 2: Double payment is immediately rejected and balance remains unchanged
        await doublePayAction.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already paid*");
        account.Balance.Should().Be(750m); // Balance not debited a second time!
    }

    [Fact]
    public async Task PayBillAsync_ShouldThrowInvalidOperationException_WhenBillStatusIsNotPending()
    {
        // Arrange
        var userId = "user-1";
        var accountId = Guid.NewGuid();
        var bill = new Bill
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = 100m,
            Status = BillStatus.Cancelled
        };
        var account = new Account
        {
            Id = accountId,
            UserId = userId,
            Balance = 1000m,
            Status = AccountStatus.Active
        };

        _contextMock.Setup(c => c.Bills).ReturnsDbSet(new List<Bill> { bill });
        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { account });

        // Act
        var action = async () => await _billService.PayBillAsync(userId, bill.Id, new PayBillRequest(accountId));

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be paid because its status is Cancelled*");
    }

    [Fact]
    public async Task PayBillAsync_ShouldTriggerNotification_WhenPaymentSucceedsAndNotificationServiceIsProvided()
    {
        // Arrange
        var userId = "user-1";
        var billId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var bill = new Bill { Id = billId, UserId = userId, Amount = 150m, Status = BillStatus.Pending, BillingNumber = "BILL-99" };
        var account = new Account { Id = accountId, UserId = userId, Balance = 500m, Status = AccountStatus.Active, Currency = "EGP" };

        _contextMock.Setup(c => c.Bills).ReturnsDbSet(new List<Bill> { bill });
        _contextMock.Setup(c => c.Accounts).ReturnsDbSet(new List<Account> { account });
        _contextMock.Setup(c => c.Transactions).ReturnsDbSet(new List<Transaction>());

        var notificationServiceMock = new Mock<Limita.Application.Features.Notifications.Services.INotificationService>();
        var serviceWithNotifications = new BillService(_contextMock.Object, notificationServiceMock.Object);

        var request = new PayBillRequest(accountId);

        // Act
        var result = await serviceWithNotifications.PayBillAsync(userId, billId, request);

        // Assert
        result.Should().NotBeNull();
        notificationServiceMock.Verify(n => n.CreateAndSendNotificationAsync(
            userId,
            "Bill Paid",
            It.Is<string>(msg => msg.Contains("BILL-99")),
            "Transaction",
            result.ReferenceNumber,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
