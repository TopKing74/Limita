using FluentValidation;
using Limita.Application.Features.Limits.Models;

namespace Limita.Application.Features.Limits.Validators;

/// <summary>
/// Validator for account limit update requests.
/// </summary>
public class UpdateAccountLimitsRequestValidator : AbstractValidator<UpdateAccountLimitsRequest>
{
    public UpdateAccountLimitsRequestValidator()
    {
        RuleFor(x => x.PerTransactionLimit)
            .GreaterThan(0)
            .WithMessage("Per-transaction limit must be greater than 0.")
            .LessThanOrEqualTo(1_000_000m)
            .WithMessage("Per-transaction limit cannot exceed 1,000,000.")
            .When(x => x.PerTransactionLimit.HasValue);

        RuleFor(x => x.DailyLimit)
            .GreaterThan(0)
            .WithMessage("Daily limit must be greater than 0.")
            .LessThanOrEqualTo(2_000_000m)
            .WithMessage("Daily limit cannot exceed 2,000,000.")
            .When(x => x.DailyLimit.HasValue);

        RuleFor(x => x.MonthlyLimit)
            .GreaterThan(0)
            .WithMessage("Monthly limit must be greater than 0.")
            .LessThanOrEqualTo(10_000_000m)
            .WithMessage("Monthly limit cannot exceed 10,000,000.")
            .When(x => x.MonthlyLimit.HasValue);

        RuleFor(x => x)
            .Must(x => !x.PerTransactionLimit.HasValue || !x.DailyLimit.HasValue || x.PerTransactionLimit <= x.DailyLimit)
            .WithMessage("Per-transaction limit cannot exceed the daily limit.")
            .Must(x => !x.DailyLimit.HasValue || !x.MonthlyLimit.HasValue || x.DailyLimit <= x.MonthlyLimit)
            .WithMessage("Daily limit cannot exceed the monthly limit.")
            .Must(x => !x.PerTransactionLimit.HasValue || !x.MonthlyLimit.HasValue || x.PerTransactionLimit <= x.MonthlyLimit)
            .WithMessage("Per-transaction limit cannot exceed the monthly limit.")
            .Must(x => x.PerTransactionLimit.HasValue || x.DailyLimit.HasValue || x.MonthlyLimit.HasValue)
            .WithMessage("At least one limit field (PerTransactionLimit, DailyLimit, or MonthlyLimit) must be provided.");
    }
}
