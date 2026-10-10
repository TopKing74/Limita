using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Limita.Application.Common;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Bills.Models;
using Limita.Application.Features.Bills.Services;

namespace Limita.API.Controllers;

[Authorize]
public class BillsController : BaseApiController
{
    private readonly IBillService _billService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<CreateBillRequest> _createBillValidator;

    public BillsController(
        IBillService billService,
        ICurrentUserService currentUserService,
        IValidator<CreateBillRequest> createBillValidator)
    {
        _billService = billService;
        _currentUserService = currentUserService;
        _createBillValidator = createBillValidator;
    }

    /// <summary>
    /// Create a new bill for the authenticated user.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<BillDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<BillDto>>> CreateBill(
        [FromBody] CreateBillRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var validationResult = await _createBillValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        try
        {
            var bill = await _billService.CreateBillAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetUserBills), new { }, ApiResponse<BillDto>.Ok(bill, "Bill created successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Get all bills for the authenticated user, optionally filtered by provider.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<BillDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IEnumerable<BillDto>>>> GetUserBills(
        [FromQuery] Guid? providerId,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var bills = await _billService.GetUserBillsAsync(userId, providerId, cancellationToken);
        return Ok(ApiResponse<IEnumerable<BillDto>>.Ok(bills, "Bills retrieved successfully."));
    }

    private string? GetRequiredUserId() =>
        string.IsNullOrWhiteSpace(_currentUserService.UserId) ? null : _currentUserService.UserId;
}
