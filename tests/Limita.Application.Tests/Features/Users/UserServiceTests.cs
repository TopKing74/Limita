using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using Limita.Application.Features.Users.Models;
using Limita.Application.Features.Users.Services;
using Limita.Domain.Entities;
using Xunit;

namespace Limita.Application.Tests.Features.Users;

public class UserServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object,
            null!, null!, null!, null!, null!, null!, null!, null!);

        _userService = new UserService(_userManagerMock.Object);
    }

    [Fact]
    public async Task GetProfileAsync_ShouldReturnProfileWithSplitName_WhenUserExists()
    {
        // Arrange
        var userId = "user-1";
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "john.doe@limita.bank",
            FullName = "John Doe",
            PhoneNumber = "+201000000002"
        };

        _userManagerMock.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);

        // Act
        var result = await _userService.GetProfileAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(userId);
        result.Email.Should().Be("john.doe@limita.bank");
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
        result.PhoneNumber.Should().Be("+201000000002");
    }

    [Fact]
    public async Task GetProfileAsync_ShouldReturnNull_WhenUserNotFound()
    {
        // Arrange
        _userManagerMock.Setup(m => m.FindByIdAsync("non-existent")).ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _userService.GetProfileAsync("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProfileAsync_ShouldUpdateFullNameAndPhone_WhenValid()
    {
        // Arrange
        var userId = "user-1";
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "john@limita.bank",
            FullName = "Old Name",
            PhoneNumber = "111"
        };

        _userManagerMock.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var request = new UpdateProfileRequest("Jane", "Smith", "+201222333444");

        // Act
        var result = await _userService.UpdateProfileAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result!.FirstName.Should().Be("Jane");
        result.LastName.Should().Be("Smith");
        result.PhoneNumber.Should().Be("+201222333444");

        user.FullName.Should().Be("Jane Smith");
        user.PhoneNumber.Should().Be("+201222333444");
        _userManagerMock.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task UpdateProfileAsync_ShouldThrowInvalidOperationException_WhenUpdateFails()
    {
        // Arrange
        var userId = "user-1";
        var user = new ApplicationUser { Id = userId };

        _userManagerMock.Setup(m => m.FindByIdAsync(userId)).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Concurrency error" }));

        var request = new UpdateProfileRequest("Jane", "Smith", null);

        // Act
        var act = async () => await _userService.UpdateProfileAsync(userId, request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Concurrency error*");
    }
}
