using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Limita.Application.Common;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Users.Models;
using Limita.Application.Features.Users.Services;

namespace Limita.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : BaseApiController
{
    private readonly IUserService _userService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<UpdateProfileRequest> _updateProfileValidator;

    public UsersController(
        IUserService userService,
        ICurrentUserService currentUserService,
        IValidator<UpdateProfileRequest> updateProfileValidator)
    {
        _userService = userService;
        _currentUserService = currentUserService;
        _updateProfileValidator = updateProfileValidator;
    }

    /// <summary>
    /// Retrieve current authenticated user profile.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetCurrentUser(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var profile = await _userService.GetProfileAsync(userId, cancellationToken);
        if (profile is null)
        {
            return NotFound(ApiResponse.Fail("User profile not found."));
        }

        return Ok(ApiResponse<UserProfileDto>.Ok(profile, "User profile retrieved successfully."));
    }

    /// <summary>
    /// Update current authenticated user profile information.
    /// </summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> UpdateCurrentUser(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var validationResult = await _updateProfileValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var profile = await _userService.UpdateProfileAsync(userId, request, cancellationToken);
        if (profile is null)
        {
            return NotFound(ApiResponse.Fail("User profile not found."));
        }

        return Ok(ApiResponse<UserProfileDto>.Ok(profile, "User profile updated successfully."));
    }

    private string? GetCurrentUserId()
    {
        if (!string.IsNullOrWhiteSpace(_currentUserService.UserId))
        {
            return _currentUserService.UserId;
        }

        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")?.Value;
    }
}
