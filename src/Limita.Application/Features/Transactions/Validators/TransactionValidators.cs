using FluentValidation;
using Limita.Application.Features.Transactions.Models;

namespace Limita.Application.Features.Transactions.Validators;

/// <summary>
/// Validator for transaction filter query parameters.
/// </summary>
public class TransactionFilterValidator : AbstractValidator<TransactionFilterRequest>
{
    public TransactionFilterValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page number must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage("Page size must be between 1 and 50.");

        RuleFor(x => x)
            .Must(x => !x.FromDate.HasValue || !x.ToDate.HasValue || x.FromDate.Value <= x.ToDate.Value)
            .WithMessage("FromDate cannot be later than ToDate.");
    }
}
