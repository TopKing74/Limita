using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Limita.Application.Common;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Beneficiaries.Models;
using Limita.Application.Features.Beneficiaries.Services;

namespace Limita.API.Controllers;

/// <summary>
/// Endpoints for managing the authenticated user's transfer recipient contacts (beneficiaries).
/// </summary>
[Authorize]
public class BeneficiariesController : BaseApiController
{
    private readonly IBeneficiaryService _beneficiaryService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<CreateBeneficiaryRequest> _createValidator;
    private readonly IValidator<UpdateBeneficiaryRequest> _updateValidator;

    public BeneficiariesController(
        IBeneficiaryService beneficiaryService,
        ICurrentUserService currentUserService,
        IValidator<CreateBeneficiaryRequest> createValidator,
        IValidator<UpdateBeneficiaryRequest> updateValidator)
    {
        _beneficiaryService = beneficiaryService;
        _currentUserService = currentUserService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>
    /// Retrieve all saved transfer recipients belonging to the authenticated user.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of saved beneficiaries sorted with favorites first.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BeneficiaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BeneficiaryDto>>>> GetBeneficiaries(
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var beneficiaries = await _beneficiaryService.GetUserBeneficiariesAsync(userId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<BeneficiaryDto>>.Ok(
            beneficiaries,
            "Beneficiaries retrieved successfully."));
    }

    /// <summary>
    /// Retrieve details of a specific beneficiary by ID.
    /// </summary>
    /// <param name="id">The unique GUID of the beneficiary.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The beneficiary details if found and owned by the current user.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BeneficiaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<BeneficiaryDto>>> GetBeneficiaryById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var beneficiary = await _beneficiaryService.GetBeneficiaryByIdAsync(userId, id, cancellationToken);
        if (beneficiary is null)
        {
            return NotFound(ApiResponse.Fail("Beneficiary not found or you do not have permission to view it."));
        }

        return Ok(ApiResponse<BeneficiaryDto>.Ok(beneficiary, "Beneficiary details retrieved successfully."));
    }

    /// <summary>
    /// Add a new recipient to the user's saved transfer contacts.
    /// </summary>
    /// <param name="request">The beneficiary data to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created beneficiary record.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<BeneficiaryDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<BeneficiaryDto>>> CreateBeneficiary(
        [FromBody] CreateBeneficiaryRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var validationResult = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        try
        {
            var created = await _beneficiaryService.CreateBeneficiaryAsync(userId, request, cancellationToken);

            return CreatedAtAction(
                nameof(GetBeneficiaryById),
                new { id = created.Id },
                ApiResponse<BeneficiaryDto>.Ok(created, "Beneficiary added successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Update details of an existing saved recipient (name, bank, phone, favorite status).
    /// </summary>
    /// <param name="id">The unique GUID of the beneficiary.</param>
    /// <param name="request">The updated beneficiary data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated beneficiary record.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BeneficiaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<BeneficiaryDto>>> UpdateBeneficiary(
        Guid id,
        [FromBody] UpdateBeneficiaryRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var validationResult = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var updated = await _beneficiaryService.UpdateBeneficiaryAsync(userId, id, request, cancellationToken);
        if (updated is null)
        {
            return NotFound(ApiResponse.Fail("Beneficiary not found or you do not have permission to modify it."));
        }

        return Ok(ApiResponse<BeneficiaryDto>.Ok(updated, "Beneficiary updated successfully."));
    }

    /// <summary>
    /// Remove a recipient from the user's saved transfer contacts.
    /// </summary>
    /// <param name="id">The unique GUID of the beneficiary to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A confirmation message indicating successful deletion.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse>> DeleteBeneficiary(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var deleted = await _beneficiaryService.DeleteBeneficiaryAsync(userId, id, cancellationToken);
        if (!deleted)
        {
            return NotFound(ApiResponse.Fail("Beneficiary not found or you do not have permission to delete it."));
        }

        return Ok(ApiResponse.Ok("Beneficiary removed successfully from your contacts."));
    }

    private string? GetRequiredUserId() =>
        string.IsNullOrWhiteSpace(_currentUserService.UserId) ? null : _currentUserService.UserId;
}
