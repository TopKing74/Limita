using FluentAssertions;
using Limita.Application.Features.Users.Models;
using Limita.Application.Features.Users.Validators;
using Xunit;

namespace Limita.Application.Tests.Features.Users;

public class UserValidatorsTests
{
    private readonly UpdateProfileRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenAllFieldsAreValid()
    {
        var request = new UpdateProfileRequest("Adham", "Samaan", "+201000000000");
        var result = _validator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Doe")]
    [InlineData("   ", "Doe")]
    [InlineData(null, "Doe")]
    public void Validate_ShouldFail_WhenFirstNameIsEmpty(string? firstName, string lastName)
    {
        var request = new UpdateProfileRequest(firstName!, lastName, null);
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.FirstName));
    }

    [Theory]
    [InlineData("John", "")]
    [InlineData("John", "   ")]
    [InlineData("John", null)]
    public void Validate_ShouldFail_WhenLastNameIsEmpty(string firstName, string? lastName)
    {
        var request = new UpdateProfileRequest(firstName, lastName!, null);
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.LastName));
    }

    [Fact]
    public void Validate_ShouldFail_WhenPhoneExceeds20Chars()
    {
        var request = new UpdateProfileRequest("John", "Doe", new string('1', 21));
        var result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.PhoneNumber));
    }
}
