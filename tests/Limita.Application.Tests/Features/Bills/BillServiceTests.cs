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
}
