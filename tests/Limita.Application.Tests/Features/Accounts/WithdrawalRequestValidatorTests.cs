using FluentValidation.TestHelper;
using Limita.Application.Features.Accounts.Models;
using Limita.Application.Features.Accounts.Validators;
using Xunit;

namespace Limita.Application.Tests.Features.Accounts;

public class WithdrawalRequestValidatorTests
{
    private readonly WithdrawalRequestValidator _validator;

    public WithdrawalRequestValidatorTests()
    {
        _validator = new WithdrawalRequestValidator();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(-0.01)]
    public void Should_Have_Error_When_Amount_Is_Zero_Or_Negative(decimal amount)
    {
        // Arrange
        var request = new WithdrawalRequest(amount);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Amount)
              .WithErrorMessage("Withdrawal amount must be greater than zero.");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Amount_Is_Positive()
    {
        // Arrange
        var request = new WithdrawalRequest(100m);

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Amount);
    }
}
