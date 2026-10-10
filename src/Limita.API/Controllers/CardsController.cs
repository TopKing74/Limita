using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Limita.Application.Common;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Cards.Models;
using Limita.Application.Features.Cards.Services;

namespace Limita.API.Controllers;

[Authorize]
public class CardsController : BaseApiController
{
    private readonly ICardService _cardService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<IssueCardRequest> _issueValidator;
    private readonly IValidator<BlockCardRequest> _blockValidator;
    private readonly IValidator<UpdateCardLimitsRequest> _limitsValidator;
    private readonly IValidator<ChangeCardPinRequest> _pinValidator;

    public CardsController(
        ICardService cardService,
        ICurrentUserService currentUserService,
        IValidator<IssueCardRequest> issueValidator,
        IValidator<BlockCardRequest> blockValidator,
        IValidator<UpdateCardLimitsRequest> limitsValidator,
        IValidator<ChangeCardPinRequest> pinValidator)
    {
        _cardService = cardService;
        _currentUserService = currentUserService;
        _issueValidator = issueValidator;
        _blockValidator = blockValidator;
        _limitsValidator = limitsValidator;
        _pinValidator = pinValidator;
    }

    /// <summary>
    /// Retrieve all debit/credit cards linked to the current user's accounts.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CardSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CardSummaryDto>>>> GetCards(
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var cards = await _cardService.GetUserCardsAsync(userId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CardSummaryDto>>.Ok(cards, "Cards retrieved successfully."));
    }

    /// <summary>
    /// Get card details (masked number, expiry, status, limits).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CardDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CardDetailsDto>>> GetCardById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var card = await _cardService.GetCardByIdAsync(userId, id, cancellationToken);
        if (card is null)
        {
            return NotFound(ApiResponse.Fail("Card not found or you do not have access to it."));
        }

        return Ok(ApiResponse<CardDetailsDto>.Ok(card, "Card details retrieved successfully."));
    }

    /// <summary>
    /// Request / issue a new card (virtual or physical) on an owned account.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CardDetailsDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CardDetailsDto>>> IssueCard(
        [FromBody] IssueCardRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var validation = await _issueValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse.Fail("Validation failed.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        var card = await _cardService.IssueCardAsync(userId, request, cancellationToken);

        return CreatedAtAction(
            nameof(GetCardById),
            new { id = card.Id },
            ApiResponse<CardDetailsDto>.Ok(card, "Card issued successfully."));
    }

    /// <summary>
    /// Freeze / block a card temporarily or permanently.
    /// </summary>
    [HttpPatch("{id:guid}/block")]
    [ProducesResponseType(typeof(ApiResponse<CardDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CardDetailsDto>>> BlockCard(
        Guid id,
        [FromBody] BlockCardRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var validation = await _blockValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse.Fail("Validation failed.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        var card = await _cardService.BlockCardAsync(userId, id, request, cancellationToken);
        if (card is null)
        {
            return NotFound(ApiResponse.Fail("Card not found or you do not have access to it."));
        }

        return Ok(ApiResponse<CardDetailsDto>.Ok(card, "Card blocked successfully."));
    }

    /// <summary>
    /// Unfreeze / reactivate a temporarily locked card.
    /// </summary>
    [HttpPatch("{id:guid}/unblock")]
    [ProducesResponseType(typeof(ApiResponse<CardDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CardDetailsDto>>> UnblockCard(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var card = await _cardService.UnblockCardAsync(userId, id, cancellationToken);
        if (card is null)
        {
            return NotFound(ApiResponse.Fail("Card not found or you do not have access to it."));
        }

        return Ok(ApiResponse<CardDetailsDto>.Ok(card, "Card unblocked successfully."));
    }

    /// <summary>
    /// Update daily ATM withdrawal and POS spending limits.
    /// </summary>
    [HttpPut("{id:guid}/limits")]
    [ProducesResponseType(typeof(ApiResponse<CardDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CardDetailsDto>>> UpdateLimits(
        Guid id,
        [FromBody] UpdateCardLimitsRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var validation = await _limitsValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse.Fail("Validation failed.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        var card = await _cardService.UpdateLimitsAsync(userId, id, request, cancellationToken);
        if (card is null)
        {
            return NotFound(ApiResponse.Fail("Card not found or you do not have access to it."));
        }

        return Ok(ApiResponse<CardDetailsDto>.Ok(card, "Card limits updated successfully."));
    }

    /// <summary>
    /// Change or set the card ATM PIN code. PIN is stored hashed and never returned.
    /// </summary>
    [HttpPatch("{id:guid}/pin")]
    [ProducesResponseType(typeof(ApiResponse<CardDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CardDetailsDto>>> ChangePin(
        Guid id,
        [FromBody] ChangeCardPinRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var validation = await _pinValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(ApiResponse.Fail("Validation failed.", validation.Errors.Select(e => e.ErrorMessage).ToList()));
        }

        var card = await _cardService.ChangePinAsync(userId, id, request, cancellationToken);
        if (card is null)
        {
            return NotFound(ApiResponse.Fail("Card not found or you do not have access to it."));
        }

        return Ok(ApiResponse<CardDetailsDto>.Ok(card, "Card PIN updated successfully."));
    }

    private string? GetRequiredUserId() =>
        string.IsNullOrWhiteSpace(_currentUserService.UserId) ? null : _currentUserService.UserId;
}
