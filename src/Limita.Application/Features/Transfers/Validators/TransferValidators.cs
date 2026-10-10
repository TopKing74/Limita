using FluentValidation;
using Limita.Application.Features.Transfers.Models;

namespace Limita.Application.Features.Transfers.Validators;

/// <summary>
/// Validator for transfer creation requests.
/// </summary>
public class CreateTransferValidator : AbstractValidator<CreateTransferRequest>
{
    public CreateTransferValidator()
    {
        RuleFor(x => x.SourceAccountId)
            .NotEmpty()
            .WithMessage("Source account ID is required.");

        RuleFor(x => x.DestinationAccountNumber)
            .NotEmpty()
            .WithMessage("Destination account number is required.")
            .MaximumLength(50)
            .WithMessage("Destination account number cannot exceed 50 characters.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Transfer amount must be strictly greater than zero.");

        RuleFor(x => x.Description)
            .MaximumLength(255)
            .WithMessage("Description cannot exceed 255 characters.");
    }
}
