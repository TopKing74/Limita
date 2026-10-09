using FluentValidation;
using Limita.Application.Features.BillProviders.Models;

namespace Limita.Application.Features.BillProviders.Validators;

public class CreateBillProviderRequestValidator : AbstractValidator<CreateBillProviderRequest>
{
    public CreateBillProviderRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage("Category must be a valid BillCategory.");
    }
}
