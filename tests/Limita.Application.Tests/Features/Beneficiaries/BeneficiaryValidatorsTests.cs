using FluentAssertions;
using Limita.Application.Features.Beneficiaries.Models;
using Limita.Application.Features.Beneficiaries.Validators;
using Xunit;

namespace Limita.Application.Tests.Features.Beneficiaries;

public class BeneficiaryValidatorsTests
{
    private readonly CreateBeneficiaryRequestValidator _createValidator = new();
    private readonly UpdateBeneficiaryRequestValidator _updateValidator = new();

    [Fact]
    public void CreateValidator_ShouldPass_WhenAllFieldsAreValid()
    {
        var request = new CreateBeneficiaryRequest(
            FullName: "John Doe",
            AccountNumber: "EG12345678901234",
            BankName: "Limita Bank",
            PhoneNumber: "+201000000000",
            IsFavorite: true);

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CreateValidator_ShouldFail_WhenFullNameIsEmpty(string? name)
    {
        var request = new CreateBeneficiaryRequest(
            FullName: name!,
            AccountNumber: "EG1234567890",
            BankName: "Limita Bank",
            PhoneNumber: null,
            IsFavorite: false);

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.FullName));
    }

    [Fact]
    public void CreateValidator_ShouldFail_WhenFullNameExceeds150Chars()
    {
        var longName = new string('A', 151);
        var request = new CreateBeneficiaryRequest(
            FullName: longName,
            AccountNumber: "EG1234567890",
            BankName: "Limita Bank",
            PhoneNumber: null,
            IsFavorite: false);

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("150"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234")] // Less than 5 chars
    public void CreateValidator_ShouldFail_WhenAccountNumberIsTooShort(string acc)
    {
        var request = new CreateBeneficiaryRequest(
            FullName: "Valid Name",
            AccountNumber: acc,
            BankName: "Limita Bank",
            PhoneNumber: null,
            IsFavorite: false);

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.AccountNumber));
    }

    [Fact]
    public void CreateValidator_ShouldFail_WhenBankNameIsEmpty()
    {
        var request = new CreateBeneficiaryRequest(
            FullName: "Valid Name",
            AccountNumber: "EG1234567890",
            BankName: "",
            PhoneNumber: null,
            IsFavorite: false);

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.BankName));
    }

    [Fact]
    public void CreateValidator_ShouldFail_WhenPhoneExceeds20Chars()
    {
        var request = new CreateBeneficiaryRequest(
            FullName: "Valid Name",
            AccountNumber: "EG1234567890",
            BankName: "Limita Bank",
            PhoneNumber: new string('1', 21),
            IsFavorite: false);

        var result = _createValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.PhoneNumber));
    }

    [Fact]
    public void UpdateValidator_ShouldPass_WhenAllFieldsAreValid()
    {
        var request = new UpdateBeneficiaryRequest(
            FullName: "Jane Doe",
            BankName: "Commercial International Bank",
            PhoneNumber: "+201112223334",
            IsFavorite: false);

        var result = _updateValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateValidator_ShouldFail_WhenFullNameOrBankNameEmpty()
    {
        var request = new UpdateBeneficiaryRequest("", "", null, false);
        var result = _updateValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.FullName));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.BankName));
    }
}
