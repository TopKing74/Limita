using FluentValidation.TestHelper;
using Limita.Application.Features.Accounts.Models;
using Limita.Application.Features.Accounts.Validators;
using Xunit;

namespace Limita.Application.Tests.Features.Accounts;

public class RechargeRequestValidatorTests
{
    private readonly RechargeRequestValidator _validator;

    public RechargeRequestValidatorTests()
    {
        _validator = new RechargeRequestValidator();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(-0.01)]
    public void Should_Have_Error_When_Amount_Is_Zero_Or_Negative(decimal amount)
    {
        // Arrange
        var request = new RechargeRequest(amount);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Amount)
              .WithErrorMessage("Recharge amount must be greater than zero.");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Amount_Is_Positive()
    {
        // Arrange
        var request = new RechargeRequest(100m);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Amount);
    }
}
