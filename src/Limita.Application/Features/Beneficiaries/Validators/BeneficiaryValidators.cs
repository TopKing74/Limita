using FluentValidation;
using Limita.Application.Features.Beneficiaries.Models;

namespace Limita.Application.Features.Beneficiaries.Validators;

/// <summary>
/// Validator enforcing input constraints when saving a new transfer beneficiary.
/// </summary>
public class CreateBeneficiaryRequestValidator : AbstractValidator<CreateBeneficiaryRequest>
{
    public CreateBeneficiaryRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Recipient full name is required.")
            .MaximumLength(150).WithMessage("Recipient full name must not exceed 150 characters.");

        RuleFor(x => x.AccountNumber)
            .NotEmpty().WithMessage("Account number or IBAN is required.")
            .MinimumLength(5).WithMessage("Account number must be at least 5 characters long.")
            .MaximumLength(50).WithMessage("Account number must not exceed 50 characters.");

        RuleFor(x => x.BankName)
            .NotEmpty().WithMessage("Bank name is required.")
            .MaximumLength(100).WithMessage("Bank name must not exceed 100 characters.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(20).WithMessage("Phone number must not exceed 20 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
    }
}

/// <summary>
/// Validator enforcing input constraints when modifying an existing transfer beneficiary.
/// </summary>
public class UpdateBeneficiaryRequestValidator : AbstractValidator<UpdateBeneficiaryRequest>
{
    public UpdateBeneficiaryRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Recipient full name is required.")
            .MaximumLength(150).WithMessage("Recipient full name must not exceed 150 characters.");

        RuleFor(x => x.BankName)
            .NotEmpty().WithMessage("Bank name is required.")
            .MaximumLength(100).WithMessage("Bank name must not exceed 100 characters.");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(20).WithMessage("Phone number must not exceed 20 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
    }
}
