using System.Text.RegularExpressions;
using FluentValidation;
using Limita.Application.Features.Recharge.Models;

namespace Limita.Application.Features.Recharge.Validators;

public partial class MobileRechargeRequestValidator : AbstractValidator<MobileRechargeRequest>
{
    private static readonly Regex EgyptianPhoneRegex = new(
        @"^(\+?20|0)?1[0125][0-9]{8}$",
        RegexOptions.Compiled);

    public MobileRechargeRequestValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty()
            .WithMessage("AccountId is required.");

        RuleFor(x => x.PhoneNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Phone number is required.")
            .Must(phone => !string.IsNullOrWhiteSpace(phone) && EgyptianPhoneRegex.IsMatch(phone.Trim()))
            .WithMessage("Phone number must be a valid Egyptian mobile number (Vodafone: 010, Etisalat: 011, Orange: 012, WE: 015).");

        RuleFor(x => x.Operator)
            .IsInEnum()
            .WithMessage("Mobile operator must be one of: Vodafone, Orange, Etisalat, WE.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Recharge amount must be greater than zero.");
    }
}
