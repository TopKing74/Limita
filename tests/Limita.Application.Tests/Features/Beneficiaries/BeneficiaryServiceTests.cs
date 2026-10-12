using FluentAssertions;
using Moq;
using Moq.EntityFrameworkCore;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Beneficiaries.Models;
using Limita.Application.Features.Beneficiaries.Services;
using Limita.Domain.Entities;
using Xunit;

namespace Limita.Application.Tests.Features.Beneficiaries;

public class BeneficiaryServiceTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly BeneficiaryService _beneficiaryService;

    public BeneficiaryServiceTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _beneficiaryService = new BeneficiaryService(_contextMock.Object);
    }

    [Fact]
    public async Task GetUserBeneficiariesAsync_ShouldReturnOnlyUserOwnedBeneficiaries_SortedByFavoriteAndDate()
    {
        // Arrange
        var userId = "user-1";
        var foreignUserId = "user-2";

        var b1 = new Beneficiary
        {
            Id = Guid.NewGuid(),
            OwnerUserId = userId,
            FullName = "Alice Smith",
            AccountNumber = "ACC111",
            BankName = "Limita Bank",
            IsFavorite = false,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var b2 = new Beneficiary
        {
            Id = Guid.NewGuid(),
            OwnerUserId = userId,
            FullName = "Bob Jones",
            AccountNumber = "ACC222",
            BankName = "Limita Bank",
            IsFavorite = true,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var foreignB = new Beneficiary
        {
            Id = Guid.NewGuid(),
            OwnerUserId = foreignUserId,
            FullName = "Foreign Contact",
            AccountNumber = "ACC333",
            BankName = "Other Bank",
            IsFavorite = true
        };

        _contextMock.Setup(c => c.Beneficiaries)
            .ReturnsDbSet(new List<Beneficiary> { b1, b2, foreignB });

        // Act
        var result = await _beneficiaryService.GetUserBeneficiariesAsync(userId);

        // Assert
        result.Should().HaveCount(2);
        result[0].FullName.Should().Be("Bob Jones"); // Favorite first
        result[0].IsFavorite.Should().BeTrue();
        result[1].FullName.Should().Be("Alice Smith");
        result.Should().NotContain(b => b.FullName == "Foreign Contact");
    }

    [Fact]
    public async Task GetBeneficiaryByIdAsync_ShouldReturnDto_WhenBeneficiaryBelongsToUser()
    {
        // Arrange
        var userId = "user-1";
        var beneficiaryId = Guid.NewGuid();
        var beneficiary = new Beneficiary
        {
            Id = beneficiaryId,
            OwnerUserId = userId,
            FullName = "Alice Smith",
            AccountNumber = "ACC111",
            BankName = "Limita Bank",
            PhoneNumber = "+201000000001",
            IsFavorite = true,
            CreatedAt = DateTime.UtcNow
        };

        _contextMock.Setup(c => c.Beneficiaries)
            .ReturnsDbSet(new List<Beneficiary> { beneficiary });

        // Act
        var result = await _beneficiaryService.GetBeneficiaryByIdAsync(userId, beneficiaryId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(beneficiaryId);
        result.FullName.Should().Be("Alice Smith");
        result.AccountNumber.Should().Be("ACC111");
        result.PhoneNumber.Should().Be("+201000000001");
        result.IsFavorite.Should().BeTrue();
    }

    [Fact]
    public async Task GetBeneficiaryByIdAsync_ShouldReturnNull_WhenBeneficiaryBelongsToAnotherUser()
    {
        // Arrange
        var userId = "user-1";
        var beneficiaryId = Guid.NewGuid();
        var beneficiary = new Beneficiary
        {
            Id = beneficiaryId,
            OwnerUserId = "different-user",
            FullName = "Secret Contact",
            AccountNumber = "ACC999",
            BankName = "Limita Bank"
        };

        _contextMock.Setup(c => c.Beneficiaries)
            .ReturnsDbSet(new List<Beneficiary> { beneficiary });

        // Act
        var result = await _beneficiaryService.GetBeneficiaryByIdAsync(userId, beneficiaryId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateBeneficiaryAsync_ShouldNormalizeAndSaveBeneficiary_WhenValid()
    {
        // Arrange
        var userId = "user-1";
        var request = new CreateBeneficiaryRequest(
            FullName: "  Charlie Brown  ",
            AccountNumber: "  eg123456789  ",
            BankName: " National Bank ",
            PhoneNumber: " +201111223344 ",
            IsFavorite: true);

        var beneficiaries = new List<Beneficiary>();
        _contextMock.Setup(c => c.Beneficiaries).ReturnsDbSet(beneficiaries);
        _contextMock.Setup(c => c.Beneficiaries.AddAsync(It.IsAny<Beneficiary>(), It.IsAny<CancellationToken>()))
            .Callback<Beneficiary, CancellationToken>((b, _) => beneficiaries.Add(b));

        // Act
        var result = await _beneficiaryService.CreateBeneficiaryAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.FullName.Should().Be("Charlie Brown");
        result.AccountNumber.Should().Be("EG123456789"); // Normalized uppercase & trimmed
        result.BankName.Should().Be("National Bank");
        result.PhoneNumber.Should().Be("+201111223344");
        result.IsFavorite.Should().BeTrue();

        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateBeneficiaryAsync_ShouldThrowInvalidOperationException_WhenDuplicateAccountNumberForSameUser()
    {
        // Arrange
        var userId = "user-1";
        var existingBeneficiary = new Beneficiary
        {
            Id = Guid.NewGuid(),
            OwnerUserId = userId,
            AccountNumber = "EG123456789",
            FullName = "Original Contact",
            BankName = "Bank A"
        };

        _contextMock.Setup(c => c.Beneficiaries)
            .ReturnsDbSet(new List<Beneficiary> { existingBeneficiary });

        var request = new CreateBeneficiaryRequest(
            FullName: "Duplicate Contact",
            AccountNumber: "eg123456789", // Same account number with lowercase
            BankName: "Bank B",
            PhoneNumber: null,
            IsFavorite: false);

        // Act
        var act = async () => await _beneficiaryService.CreateBeneficiaryAsync(userId, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already saved*");
    }

    [Fact]
    public async Task UpdateBeneficiaryAsync_ShouldUpdateFieldsAndTimestamp_WhenOwnedByUser()
    {
        // Arrange
        var userId = "user-1";
        var beneficiaryId = Guid.NewGuid();
        var existingBeneficiary = new Beneficiary
        {
            Id = beneficiaryId,
            OwnerUserId = userId,
            FullName = "Old Name",
            AccountNumber = "ACC111",
            BankName = "Old Bank",
            PhoneNumber = "111",
            IsFavorite = false,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _contextMock.Setup(c => c.Beneficiaries)
            .ReturnsDbSet(new List<Beneficiary> { existingBeneficiary });

        var request = new UpdateBeneficiaryRequest(
            FullName: "New Name",
            BankName: "New Bank",
            PhoneNumber: "222",
            IsFavorite: true);

        // Act
        var result = await _beneficiaryService.UpdateBeneficiaryAsync(userId, beneficiaryId, request);

        // Assert
        result.Should().NotBeNull();
        result!.FullName.Should().Be("New Name");
        result.BankName.Should().Be("New Bank");
        result.PhoneNumber.Should().Be("222");
        result.IsFavorite.Should().BeTrue();
        result.UpdatedAt.Should().NotBeNull();

        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateBeneficiaryAsync_ShouldReturnNull_WhenBeneficiaryBelongsToDifferentUser()
    {
        // Arrange
        var beneficiaryId = Guid.NewGuid();
        var existingBeneficiary = new Beneficiary
        {
            Id = beneficiaryId,
            OwnerUserId = "other-user",
            FullName = "Other's Contact",
            AccountNumber = "ACC111",
            BankName = "Bank"
        };

        _contextMock.Setup(c => c.Beneficiaries)
            .ReturnsDbSet(new List<Beneficiary> { existingBeneficiary });

        var request = new UpdateBeneficiaryRequest("New Name", "New Bank", null, false);

        // Act
        var result = await _beneficiaryService.UpdateBeneficiaryAsync("user-1", beneficiaryId, request);

        // Assert
        result.Should().BeNull();
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteBeneficiaryAsync_ShouldRemoveAndReturnTrue_WhenOwnedByUser()
    {
        // Arrange
        var userId = "user-1";
        var beneficiaryId = Guid.NewGuid();
        var beneficiary = new Beneficiary
        {
            Id = beneficiaryId,
            OwnerUserId = userId,
            FullName = "To Delete",
            AccountNumber = "ACC111",
            BankName = "Bank"
        };

        _contextMock.Setup(c => c.Beneficiaries)
            .ReturnsDbSet(new List<Beneficiary> { beneficiary });

        // Act
        var result = await _beneficiaryService.DeleteBeneficiaryAsync(userId, beneficiaryId);

        // Assert
        result.Should().BeTrue();
        _contextMock.Verify(c => c.Beneficiaries.Remove(beneficiary), Times.Once);
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteBeneficiaryAsync_ShouldReturnFalse_WhenNotOwnedOrNotFound()
    {
        // Arrange
        var beneficiaryId = Guid.NewGuid();
        var beneficiary = new Beneficiary
        {
            Id = beneficiaryId,
            OwnerUserId = "other-user",
            FullName = "Not Mine",
            AccountNumber = "ACC111",
            BankName = "Bank"
        };

        _contextMock.Setup(c => c.Beneficiaries)
            .ReturnsDbSet(new List<Beneficiary> { beneficiary });

        // Act
        var result = await _beneficiaryService.DeleteBeneficiaryAsync("user-1", beneficiaryId);

        // Assert
        result.Should().BeFalse();
        _contextMock.Verify(c => c.Beneficiaries.Remove(It.IsAny<Beneficiary>()), Times.Never);
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
