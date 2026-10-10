using FluentValidation;
using Limita.Application.Features.Bills.Models;

namespace Limita.Application.Features.Bills.Validators;

public class CreateBillRequestValidator : AbstractValidator<CreateBillRequest>
{
    public CreateBillRequestValidator()
    {
        RuleFor(x => x.ProviderId)
            .NotEmpty().WithMessage("ProviderId is required.");

        RuleFor(x => x.BillingNumber)
            .NotEmpty().WithMessage("BillingNumber is required.")
            .MaximumLength(50).WithMessage("BillingNumber must not exceed 50 characters.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.");

        RuleFor(x => x.DueDate)
            .NotEmpty().WithMessage("DueDate is required.")
            .GreaterThan(DateTime.UtcNow.Date).WithMessage("DueDate must be in the future.");
    }
}
