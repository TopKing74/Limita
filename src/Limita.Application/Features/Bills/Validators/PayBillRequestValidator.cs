using FluentValidation;
using Limita.Application.Features.Bills.Models;

namespace Limita.Application.Features.Bills.Validators;

public class PayBillRequestValidator : AbstractValidator<PayBillRequest>
{
    public PayBillRequestValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty()
            .WithMessage("AccountId is required.");
    }
}
