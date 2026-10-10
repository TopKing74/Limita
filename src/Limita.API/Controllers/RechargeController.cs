using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Limita.Application.Common;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Recharge.Models;
using Limita.Application.Features.Recharge.Services;

namespace Limita.API.Controllers;

[Authorize]
public class RechargeController : BaseApiController
{
    private readonly IMobileRechargeService _rechargeService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<MobileRechargeRequest> _validator;

    public RechargeController(
        IMobileRechargeService rechargeService,
        ICurrentUserService currentUserService,
        IValidator<MobileRechargeRequest> validator)
    {
        _rechargeService = rechargeService;
        _currentUserService = currentUserService;
        _validator = validator;
    }

    /// <summary>
    /// Execute mobile operator balance recharge (REC-01: Vodafone, Orange, Etisalat, WE).
    /// </summary>
    /// <param name="request">Mobile recharge details (AccountId, PhoneNumber, Operator, Amount).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recharge confirmation and ledger transaction details.</returns>
    [HttpPost]
    [HttpPost("mobile")]
    [ProducesResponseType(typeof(ApiResponse<MobileRechargeResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<MobileRechargeResponseDto>>> RechargeMobile(
        [FromBody] MobileRechargeRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var result = await _rechargeService.RechargeMobileAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<MobileRechargeResponseDto>.Ok(result, "Mobile recharge completed successfully."));
    }

    private string? GetRequiredUserId() =>
        string.IsNullOrWhiteSpace(_currentUserService.UserId) ? null : _currentUserService.UserId;
}
