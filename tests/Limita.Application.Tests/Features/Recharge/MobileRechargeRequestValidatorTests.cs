using FluentValidation.TestHelper;
using Limita.Application.Features.Recharge.Models;
using Limita.Application.Features.Recharge.Validators;
using Limita.Domain.Enums;
using Xunit;

namespace Limita.Application.Tests.Features.Recharge;

public class MobileRechargeRequestValidatorTests
{
    private readonly MobileRechargeRequestValidator _validator;

    public MobileRechargeRequestValidatorTests()
    {
        _validator = new MobileRechargeRequestValidator();
    }

    [Theory]
    [InlineData("01012345678", MobileOperator.Vodafone)]
    [InlineData("01112345678", MobileOperator.Etisalat)]
    [InlineData("01212345678", MobileOperator.Orange)]
    [InlineData("01512345678", MobileOperator.WE)]
    [InlineData("+201012345678", MobileOperator.Vodafone)]
    public void Should_Pass_When_PhoneNumber_And_Operator_Are_Valid(string phone, MobileOperator op)
    {
        // Arrange
        var request = new MobileRechargeRequest(Guid.NewGuid(), phone, op, 50m);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("12345")]
    [InlineData("01312345678")] // 013 is not a mobile prefix in Egypt
    [InlineData("02012345678")]
    public void Should_Have_Error_When_PhoneNumber_Is_Invalid(string phone)
    {
        // Arrange
        var request = new MobileRechargeRequest(Guid.NewGuid(), phone, MobileOperator.Vodafone, 50m);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
    }

    [Fact]
    public void Should_Have_Error_When_AccountId_Is_Empty()
    {
        // Arrange
        var request = new MobileRechargeRequest(Guid.Empty, "01012345678", MobileOperator.Vodafone, 50m);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.AccountId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Should_Have_Error_When_Amount_Is_Zero_Or_Negative(decimal amount)
    {
        // Arrange
        var request = new MobileRechargeRequest(Guid.NewGuid(), "01012345678", MobileOperator.Vodafone, amount);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }
}
