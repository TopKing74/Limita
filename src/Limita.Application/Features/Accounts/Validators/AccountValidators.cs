using FluentValidation;
using Limita.Application.Features.Accounts.Models;

namespace Limita.Application.Features.Accounts.Validators;

public class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    private static readonly HashSet<string> SupportedCurrencies =
        new(StringComparer.OrdinalIgnoreCase) { "EGP", "USD", "EUR" };

    public CreateAccountRequestValidator()
    {
        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("Account type must be Checking or Savings.");

        RuleFor(x => x.Currency)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Currency is required.")
            .Length(3).WithMessage("Currency must be a 3-letter ISO 4217 code.")
            .Must(c => !string.IsNullOrWhiteSpace(c) && SupportedCurrencies.Contains(c.Trim().ToUpper()))
            .WithMessage("Supported currencies are: EGP, USD, EUR.");
    }
}

public class WithdrawalRequestValidator : AbstractValidator<WithdrawalRequest>
{
    public WithdrawalRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Withdrawal amount must be greater than zero.");
    }
}

public class RechargeRequestValidator : AbstractValidator<RechargeRequest>
{
    public RechargeRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Recharge amount must be greater than zero.");
    }
}
