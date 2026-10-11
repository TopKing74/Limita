using FluentAssertions;
using Limita.Application.Features.Auth.Models;
using Limita.Application.Features.Auth.Validators;
using Xunit;

namespace Limita.Application.Tests.Features.Auth;

public class AuthValidatorTests
{
    private readonly ChangePasswordRequestValidator _changePasswordValidator = new();
    private readonly ForgotPasswordRequestValidator _forgotPasswordValidator = new();
    private readonly ResetPasswordRequestValidator _resetPasswordValidator = new();

    [Fact]
    public void ChangePasswordValidator_ShouldPass_WhenModelIsValid()
    {
        var dto = new ChangePasswordRequestDto("CurrentPassword123!", "NewSecretPassword123!", "NewSecretPassword123!");
        var result = _changePasswordValidator.Validate(dto);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ChangePasswordValidator_ShouldFail_WhenNewPasswordEqualsCurrentPassword()
    {
        var dto = new ChangePasswordRequestDto("SamePassword123!", "SamePassword123!", "SamePassword123!");
        var result = _changePasswordValidator.Validate(dto);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("same as"));
    }

    [Fact]
    public void ChangePasswordValidator_ShouldFail_WhenConfirmPasswordDoesNotMatch()
    {
        var dto = new ChangePasswordRequestDto("Current123!", "NewPassword123!", "Different123!");
        var result = _changePasswordValidator.Validate(dto);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("must match"));
    }

    [Theory]
    [InlineData("short1!")] // < 8 chars
    [InlineData("alllowercase123!")] // no uppercase
    [InlineData("ALLUPPERCASE123!")] // no lowercase
    [InlineData("NoDigitsInPassword!")] // no digit
    [InlineData("NoSpecialChars123")] // no non-alphanumeric
    public void ChangePasswordValidator_ShouldFail_WhenPasswordDoesNotMeetComplexity(string weakPassword)
    {
        var dto = new ChangePasswordRequestDto("CurrentPassword123!", weakPassword, weakPassword);
        var result = _changePasswordValidator.Validate(dto);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ForgotPasswordValidator_ShouldPass_WhenEmailIsValid()
    {
        var dto = new ForgotPasswordRequestDto("user@example.com");
        var result = _forgotPasswordValidator.Validate(dto);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-email")]
    [InlineData("notanemail@")]
    public void ForgotPasswordValidator_ShouldFail_WhenEmailIsInvalid(string email)
    {
        var dto = new ForgotPasswordRequestDto(email);
        var result = _forgotPasswordValidator.Validate(dto);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ResetPasswordValidator_ShouldPass_WhenModelIsValid()
    {
        var dto = new ResetPasswordRequestDto("user@example.com", "123456", "NewPassword123!", "NewPassword123!");
        var result = _resetPasswordValidator.Validate(dto);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ResetPasswordValidator_ShouldFail_WhenOtpCodeIsEmpty()
    {
        var dto = new ResetPasswordRequestDto("user@example.com", "", "NewPassword123!", "NewPassword123!");
        var result = _resetPasswordValidator.Validate(dto);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ResetPasswordRequestDto.OtpCode));
    }

    [Theory]
    [InlineData("12345")] // 5 digits
    [InlineData("1234567")] // 7 digits
    [InlineData("abcdef")] // Letters
    [InlineData("12 345")] // Space
    public void ResetPasswordValidator_ShouldFail_WhenOtpCodeIsNotExactlySixDigits(string invalidOtp)
    {
        var dto = new ResetPasswordRequestDto("user@example.com", invalidOtp, "NewPassword123!", "NewPassword123!");
        var result = _resetPasswordValidator.Validate(dto);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ResetPasswordRequestDto.OtpCode));
    }

    [Fact]
    public void ResetPasswordValidator_ShouldFail_WhenConfirmPasswordDoesNotMatch()
    {
        var dto = new ResetPasswordRequestDto("user@example.com", "123456", "NewPassword123!", "Mismatch123!");
        var result = _resetPasswordValidator.Validate(dto);

        result.IsValid.Should().BeFalse();
    }
}
