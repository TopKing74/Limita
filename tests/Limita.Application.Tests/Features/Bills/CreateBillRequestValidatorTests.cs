using FluentValidation.TestHelper;
using Limita.Application.Features.Bills.Models;
using Limita.Application.Features.Bills.Validators;
using Xunit;

namespace Limita.Application.Tests.Features.Bills.Validators;

public class CreateBillRequestValidatorTests
{
    private readonly CreateBillRequestValidator _validator;

    public CreateBillRequestValidatorTests()
    {
        _validator = new CreateBillRequestValidator();
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenProviderIdIsEmpty()
    {
        var model = new CreateBillRequest(Guid.Empty, "12345", 100m, DateTime.UtcNow.AddDays(5));
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.ProviderId);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenBillingNumberIsEmpty()
    {
        var model = new CreateBillRequest(Guid.NewGuid(), "", 100m, DateTime.UtcNow.AddDays(5));
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.BillingNumber);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenAmountIsZeroOrNegative()
    {
        var model = new CreateBillRequest(Guid.NewGuid(), "12345", 0m, DateTime.UtcNow.AddDays(5));
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.Amount);

        var model2 = new CreateBillRequest(Guid.NewGuid(), "12345", -10m, DateTime.UtcNow.AddDays(5));
        var result2 = _validator.TestValidate(model2);
        result2.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Validate_ShouldHaveError_WhenDueDateIsInThePast()
    {
        var model = new CreateBillRequest(Guid.NewGuid(), "12345", 100m, DateTime.UtcNow.AddDays(-1));
        var result = _validator.TestValidate(model);
        result.ShouldHaveValidationErrorFor(x => x.DueDate);
    }

    [Fact]
    public void Validate_ShouldNotHaveError_WhenRequestIsValid()
    {
        var model = new CreateBillRequest(Guid.NewGuid(), "12345", 100m, DateTime.UtcNow.AddDays(5));
        var result = _validator.TestValidate(model);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
