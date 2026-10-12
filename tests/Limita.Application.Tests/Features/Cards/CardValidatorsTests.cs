using FluentAssertions;
using Limita.Application.Features.Cards.Models;
using Limita.Application.Features.Cards.Validators;
using Limita.Domain.Enums;
using Xunit;

namespace Limita.Application.Tests.Features.Cards;

public class CardValidatorsTests
{
    private readonly IssueCardRequestValidator _issueValidator = new();
    private readonly BlockCardRequestValidator _blockValidator = new();
    private readonly UpdateCardLimitsRequestValidator _limitsValidator = new();
    private readonly ChangeCardPinRequestValidator _pinValidator = new();

    [Fact]
    public void IssueCardValidator_ShouldPass_WhenValid()
    {
        var request = new IssueCardRequest(Guid.NewGuid(), CardType.Debit, CardForm.Virtual, IsDefault: false, CardHolderName: "JOHN DOE");
        var result = _issueValidator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void IssueCardValidator_ShouldFail_WhenAccountIdEmpty()
    {
        var request = new IssueCardRequest(Guid.Empty, CardType.Debit, CardForm.Virtual);
        var result = _issueValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.AccountId));
    }

    [Fact]
    public void BlockCardValidator_ShouldPass_WhenValid()
    {
        var request = new BlockCardRequest(CardBlockType.Temporary, "Card misplaced");
        var result = _blockValidator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void BlockCardValidator_ShouldFail_WhenReasonExceeds250Chars()
    {
        var request = new BlockCardRequest(CardBlockType.Permanent, new string('A', 251));
        var result = _blockValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.Reason));
    }

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(-100, 1000)]
    [InlineData(100_001, 1000)] // ATM limit exceeds 100k
    [InlineData(5000, 0)]
    [InlineData(5000, 500_001)] // POS limit exceeds 500k
    public void UpdateCardLimitsValidator_ShouldFail_WhenBoundariesExceeded(decimal atmLimit, decimal posLimit)
    {
        var request = new UpdateCardLimitsRequest(atmLimit, posLimit);
        var result = _limitsValidator.Validate(request);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateCardLimitsValidator_ShouldPass_WhenWithinValidRange()
    {
        var request = new UpdateCardLimitsRequest(10000m, 50000m);
        var result = _limitsValidator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("123456")]
    public void ChangePinValidator_ShouldPass_WhenPinIs4To6Digits(string pin)
    {
        var request = new ChangeCardPinRequest(pin, "0000");
        var result = _pinValidator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")] // 3 digits
    [InlineData("1234567")] // 7 digits
    [InlineData("123a")] // Non-digits
    public void ChangePinValidator_ShouldFail_WhenPinFormatInvalid(string invalidPin)
    {
        var request = new ChangeCardPinRequest(invalidPin, null);
        var result = _pinValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.NewPin));
    }
}
