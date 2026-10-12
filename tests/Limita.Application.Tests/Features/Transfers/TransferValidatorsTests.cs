using FluentAssertions;
using Limita.Application.Features.Transfers.Models;
using Limita.Application.Features.Transfers.Validators;
using Xunit;

namespace Limita.Application.Tests.Features.Transfers;

public class TransferValidatorsTests
{
    private readonly CreateTransferValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenAllFieldsAreValid()
    {
        var request = new CreateTransferRequest(
            SourceAccountId: Guid.NewGuid(),
            DestinationAccountNumber: "EG10002000300040",
            Amount: 1500m,
            Description: "Monthly transfer");

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenSourceAccountIdIsEmpty()
    {
        var request = new CreateTransferRequest(
            SourceAccountId: Guid.Empty,
            DestinationAccountNumber: "EG10002000300040",
            Amount: 100m,
            Description: null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.SourceAccountId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_ShouldFail_WhenDestinationAccountNumberIsEmpty(string? destAcc)
    {
        var request = new CreateTransferRequest(
            SourceAccountId: Guid.NewGuid(),
            DestinationAccountNumber: destAcc!,
            Amount: 100m,
            Description: null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.DestinationAccountNumber));
    }

    [Fact]
    public void Validate_ShouldFail_WhenDestinationAccountNumberExceeds50Chars()
    {
        var request = new CreateTransferRequest(
            SourceAccountId: Guid.NewGuid(),
            DestinationAccountNumber: new string('9', 51),
            Amount: 100m,
            Description: null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("50 characters"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(-0.01)]
    public void Validate_ShouldFail_WhenAmountIsZeroOrNegative(decimal amount)
    {
        var request = new CreateTransferRequest(
            SourceAccountId: Guid.NewGuid(),
            DestinationAccountNumber: "EG10002000300040",
            Amount: amount,
            Description: null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.Amount));
    }

    [Fact]
    public void Validate_ShouldFail_WhenDescriptionExceeds255Chars()
    {
        var request = new CreateTransferRequest(
            SourceAccountId: Guid.NewGuid(),
            DestinationAccountNumber: "EG10002000300040",
            Amount: 100m,
            Description: new string('x', 256));

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.Description));
    }
}
