using FluentValidation;
using Limita.Application.Features.Exchange.Models;

namespace Limita.Application.Features.Exchange.Validators;

public class ExchangeQuoteRequestValidator : AbstractValidator<ExchangeQuoteRequest>
{
    public ExchangeQuoteRequestValidator()
    {
        RuleFor(x => x.SourceAccountId)
            .NotEmpty()
            .WithMessage("Source account ID is required.");

        RuleFor(x => x.DestinationAccountId)
            .NotEmpty()
            .WithMessage("Destination account ID is required.");

        RuleFor(x => x.DestinationAccountId)
            .NotEqual(x => x.SourceAccountId)
            .WithMessage("Source and destination accounts must be different.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Exchange amount must be greater than zero.");
    }
}

public class ExecuteExchangeRequestValidator : AbstractValidator<ExecuteExchangeRequest>
{
    public ExecuteExchangeRequestValidator()
    {
        RuleFor(x => x.SourceAccountId)
            .NotEmpty()
            .WithMessage("Source account ID is required.");

        RuleFor(x => x.DestinationAccountId)
            .NotEmpty()
            .WithMessage("Destination account ID is required.");

        RuleFor(x => x.DestinationAccountId)
            .NotEqual(x => x.SourceAccountId)
            .WithMessage("Source and destination accounts must be different.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Exchange amount must be greater than zero.");
    }
}
