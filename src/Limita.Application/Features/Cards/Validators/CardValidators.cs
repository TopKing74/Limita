using FluentValidation;
using Limita.Application.Features.Cards.Models;

namespace Limita.Application.Features.Cards.Validators;

public class IssueCardRequestValidator : AbstractValidator<IssueCardRequest>
{
    public IssueCardRequestValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty().WithMessage("AccountId is required.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Card type must be Debit, Credit, or Prepaid.");

        RuleFor(x => x.Form)
            .IsInEnum().WithMessage("Card form must be Virtual or Physical.");

        RuleFor(x => x.CardHolderName)
            .MaximumLength(100).WithMessage("Card holder name cannot exceed 100 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.CardHolderName));
    }
}

public class BlockCardRequestValidator : AbstractValidator<BlockCardRequest>
{
    public BlockCardRequestValidator()
    {
        RuleFor(x => x.BlockType)
            .IsInEnum().WithMessage("Block type must be Temporary or Permanent.");

        RuleFor(x => x.Reason)
            .MaximumLength(250).WithMessage("Block reason cannot exceed 250 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Reason));
    }
}

public class UpdateCardLimitsRequestValidator : AbstractValidator<UpdateCardLimitsRequest>
{
    public UpdateCardLimitsRequestValidator()
    {
        RuleFor(x => x.DailyAtmLimit)
            .GreaterThan(0).WithMessage("Daily ATM limit must be greater than zero.")
            .LessThanOrEqualTo(100_000m).WithMessage("Daily ATM limit cannot exceed 100,000.");

        RuleFor(x => x.DailyPosLimit)
            .GreaterThan(0).WithMessage("Daily POS limit must be greater than zero.")
            .LessThanOrEqualTo(500_000m).WithMessage("Daily POS limit cannot exceed 500,000.");
    }
}

public class ChangeCardPinRequestValidator : AbstractValidator<ChangeCardPinRequest>
{
    public ChangeCardPinRequestValidator()
    {
        RuleFor(x => x.NewPin)
            .NotEmpty().WithMessage("New PIN is required.")
            .Matches(@"^\d{4,6}$").WithMessage("PIN must be 4 to 6 digits.");

        RuleFor(x => x.CurrentPin)
            .Matches(@"^\d{4,6}$").WithMessage("Current PIN must be 4 to 6 digits.")
            .When(x => !string.IsNullOrWhiteSpace(x.CurrentPin));
    }
}
