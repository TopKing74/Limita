using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using Microsoft.Extensions.Caching.Memory;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Auth.Models;
using Limita.Application.Features.Auth.Services;
using Limita.Domain.Entities;
using Xunit;

namespace Limita.Application.Tests.Features.Auth;

public class AuthServiceTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly IMemoryCache _cache;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _emailServiceMock = new Mock<IEmailService>();
        _cache = new MemoryCache(new MemoryCacheOptions());

        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object,
            null!, null!, null!, null!, null!, null!, null!, null!);

        _authService = new AuthService(_contextMock.Object, _userManagerMock.Object, _emailServiceMock.Object, _cache);
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldSucceed_WhenCredentialsAreValid()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@test.com",
            RefreshToken = "existing-refresh-token",
            RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7)
        };

        var dto = new ChangePasswordRequestDto("CurrentPassword123!", "NewSecretPassword123!", "NewSecretPassword123!");

        _userManagerMock.Setup(m => m.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _userManagerMock.Setup(m => m.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock.Setup(m => m.UpdateSecurityStampAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock.Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _authService.ChangePasswordAsync(userId, dto);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeTrue();
        result.Message.Should().Be("Password changed successfully.");
        user.RefreshToken.Should().BeNull();
        user.RefreshTokenExpiryTime.Should().BeNull();

        _userManagerMock.Verify(m => m.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword), Times.Once);
        _userManagerMock.Verify(m => m.UpdateSecurityStampAsync(user), Times.Once);
        _userManagerMock.Verify(m => m.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldFail_WhenCurrentPasswordIsIncorrect()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@test.com",
            RefreshToken = "existing-refresh-token"
        };

        var dto = new ChangePasswordRequestDto("WrongPassword123!", "NewSecretPassword123!", "NewSecretPassword123!");

        _userManagerMock.Setup(m => m.FindByIdAsync(userId))
            .ReturnsAsync(user);

        var identityError = new IdentityError { Code = "PasswordMismatch", Description = "Incorrect password." };
        _userManagerMock.Setup(m => m.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword))
            .ReturnsAsync(IdentityResult.Failed(identityError));

        // Act
        var result = await _authService.ChangePasswordAsync(userId, dto);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Incorrect password.");
        user.RefreshToken.Should().Be("existing-refresh-token");

        _userManagerMock.Verify(m => m.UpdateSecurityStampAsync(It.IsAny<ApplicationUser>()), Times.Never);
        _userManagerMock.Verify(m => m.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldFail_WhenUserNotFound()
    {
        // Arrange
        var userId = "non-existent";
        var dto = new ChangePasswordRequestDto("Current123!", "NewSecret123!", "NewSecret123!");

        _userManagerMock.Setup(m => m.FindByIdAsync(userId))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _authService.ChangePasswordAsync(userId, dto);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("User not found.");
    }

    [Fact]
    public async Task ForgotPasswordAsync_ShouldGenerateOtpAndSendEmail_WhenUserExistsAndEmailIsConfirmed()
    {
        // Arrange
        var email = "confirmed@test.com";
        var user = new ApplicationUser
        {
            Id = "user-1",
            Email = email,
            EmailConfirmed = true
        };

        var dto = new ForgotPasswordRequestDto(email);

        _userManagerMock.Setup(m => m.FindByEmailAsync(email))
            .ReturnsAsync(user);

        // Act
        var result = await _authService.ForgotPasswordAsync(dto);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeEmpty();
        result.Message.Should().Be("If this email is registered, a password reset link/code has been sent.");

        // Verify OTP was stored in cache with 6 digits and 10 minutes lifetime
        _cache.TryGetValue($"otp:reset:{email}", out PasswordResetOtp? cachedOtp).Should().BeTrue();
        cachedOtp.Should().NotBeNull();
        cachedOtp!.OtpCode.Should().MatchRegex(@"^\d{6}$");
        cachedOtp.Email.Should().Be(email);
        cachedOtp.IsUsed.Should().BeFalse();
        cachedOtp.AttemptsCount.Should().Be(0);

        _emailServiceMock.Verify(m => m.SendEmailAsync(
            email,
            "Limita Bank — Password Reset Request",
            It.Is<string>(body => body.Contains(cachedOtp.OtpCode) && body.Contains("LIMITA BANK")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPasswordAsync_ShouldReturnGenericSafeSuccess_WhenEmailDoesNotExist()
    {
        // Arrange
        var email = "doesnotexist@test.com";
        var dto = new ForgotPasswordRequestDto(email);

        _userManagerMock.Setup(m => m.FindByEmailAsync(email))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _authService.ForgotPasswordAsync(dto);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeEmpty();
        result.Message.Should().Be("If this email is registered, a password reset link/code has been sent.");

        _userManagerMock.Verify(m => m.GeneratePasswordResetTokenAsync(It.IsAny<ApplicationUser>()), Times.Never);
        _emailServiceMock.Verify(m => m.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPasswordAsync_ShouldReturnGenericSafeSuccess_WhenEmailIsNotConfirmed()
    {
        // Arrange
        var email = "unconfirmed@test.com";
        var user = new ApplicationUser
        {
            Id = "user-2",
            Email = email,
            EmailConfirmed = false
        };
        var dto = new ForgotPasswordRequestDto(email);

        _userManagerMock.Setup(m => m.FindByEmailAsync(email))
            .ReturnsAsync(user);

        // Act
        var result = await _authService.ForgotPasswordAsync(dto);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeEmpty();
        result.Message.Should().Be("If this email is registered, a password reset link/code has been sent.");

        _userManagerMock.Verify(m => m.GeneratePasswordResetTokenAsync(It.IsAny<ApplicationUser>()), Times.Never);
        _emailServiceMock.Verify(m => m.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPasswordAsync_ShouldReturnSafeSuccess_WhenEmailServiceThrowsException()
    {
        // Arrange
        var email = "confirmed@test.com";
        var user = new ApplicationUser
        {
            Id = "user-1",
            Email = email,
            EmailConfirmed = true
        };

        var expectedToken = "secure-reset-token-sample";
        var dto = new ForgotPasswordRequestDto(email);

        _userManagerMock.Setup(m => m.FindByEmailAsync(email))
            .ReturnsAsync(user);

        _userManagerMock.Setup(m => m.GeneratePasswordResetTokenAsync(user))
            .ReturnsAsync(expectedToken);

        _emailServiceMock.Setup(m => m.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Net.Sockets.SocketException());

        // Act
        var result = await _authService.ForgotPasswordAsync(dto);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeEmpty();
        result.Message.Should().Be("If this email is registered, a password reset link/code has been sent.");
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldSucceed_WhenOtpAndCredentialsAreValid()
    {
        // Arrange
        var email = "user@test.com";
        var otpCode = "654321";
        var identityToken = "internal-identity-reset-token";
        var newPassword = "NewValidPassword123!";
        var user = new ApplicationUser
        {
            Id = "user-123",
            Email = email,
            RefreshToken = "compromised-token",
            RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(2)
        };

        var otp = new PasswordResetOtp
        {
            Email = email,
            OtpCode = otpCode,
            ExpiryTime = DateTime.UtcNow.AddMinutes(10),
            AttemptsCount = 0,
            IsUsed = false
        };
        _cache.Set($"otp:reset:{email}", otp, TimeSpan.FromMinutes(10));

        var dto = new ResetPasswordRequestDto(email, otpCode, newPassword, newPassword);

        _userManagerMock.Setup(m => m.FindByEmailAsync(email))
            .ReturnsAsync(user);

        _userManagerMock.Setup(m => m.GeneratePasswordResetTokenAsync(user))
            .ReturnsAsync(identityToken);

        _userManagerMock.Setup(m => m.ResetPasswordAsync(user, identityToken, newPassword))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock.Setup(m => m.UpdateSecurityStampAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock.Setup(m => m.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _authService.ResetPasswordAsync(dto);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeTrue();
        result.Message.Should().Be("Password has been reset successfully.");
        user.RefreshToken.Should().BeNull();
        user.RefreshTokenExpiryTime.Should().BeNull();

        _userManagerMock.Verify(m => m.GeneratePasswordResetTokenAsync(user), Times.Once);
        _userManagerMock.Verify(m => m.ResetPasswordAsync(user, identityToken, newPassword), Times.Once);
        _userManagerMock.Verify(m => m.UpdateSecurityStampAsync(user), Times.Once);
        _userManagerMock.Verify(m => m.UpdateAsync(user), Times.Once);

        // Verify OTP is removed/invalidated from cache
        _cache.TryGetValue($"otp:reset:{email}", out PasswordResetOtp? _).Should().BeFalse();
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldFail_WhenOtpCodeIsInvalid()
    {
        // Arrange
        var email = "user@test.com";
        var user = new ApplicationUser { Id = "user-123", Email = email };
        var otp = new PasswordResetOtp
        {
            Email = email,
            OtpCode = "654321",
            ExpiryTime = DateTime.UtcNow.AddMinutes(10),
            AttemptsCount = 0,
            IsUsed = false
        };
        _cache.Set($"otp:reset:{email}", otp, TimeSpan.FromMinutes(10));

        var dto = new ResetPasswordRequestDto(email, "999999", "NewPassword123!", "NewPassword123!");

        _userManagerMock.Setup(m => m.FindByEmailAsync(email))
            .ReturnsAsync(user);

        // Act
        var result = await _authService.ResetPasswordAsync(dto);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid OTP code"));

        _userManagerMock.Verify(m => m.ResetPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldFail_WhenOtpIsExpired()
    {
        // Arrange
        var email = "user@test.com";
        var user = new ApplicationUser { Id = "user-123", Email = email };
        var otp = new PasswordResetOtp
        {
            Email = email,
            OtpCode = "654321",
            ExpiryTime = DateTime.UtcNow.AddMinutes(-1), // Expired
            AttemptsCount = 0,
            IsUsed = false
        };
        _cache.Set($"otp:reset:{email}", otp, TimeSpan.FromMinutes(10));

        var dto = new ResetPasswordRequestDto(email, "654321", "NewPassword123!", "NewPassword123!");

        _userManagerMock.Setup(m => m.FindByEmailAsync(email))
            .ReturnsAsync(user);

        // Act
        var result = await _authService.ResetPasswordAsync(dto);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("expired"));
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldFail_WhenAttemptsExceeded()
    {
        // Arrange
        var email = "user@test.com";
        var user = new ApplicationUser { Id = "user-123", Email = email };
        var otp = new PasswordResetOtp
        {
            Email = email,
            OtpCode = "654321",
            ExpiryTime = DateTime.UtcNow.AddMinutes(5),
            AttemptsCount = 5, // Max attempts reached
            IsUsed = false
        };
        _cache.Set($"otp:reset:{email}", otp, TimeSpan.FromMinutes(10));

        var dto = new ResetPasswordRequestDto(email, "654321", "NewPassword123!", "NewPassword123!");

        _userManagerMock.Setup(m => m.FindByEmailAsync(email))
            .ReturnsAsync(user);

        // Act
        var result = await _authService.ResetPasswordAsync(dto);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Maximum OTP verification attempts exceeded"));
    }

    [Fact]
    public async Task ResetPasswordAsync_ShouldFail_WhenUserNotFound()
    {
        // Arrange
        var email = "nonexistent@test.com";
        var dto = new ResetPasswordRequestDto(email, "123456", "NewPassword123!", "NewPassword123!");

        _userManagerMock.Setup(m => m.FindByEmailAsync(email))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _authService.ResetPasswordAsync(dto);

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("Invalid password reset request.");
    }
}
