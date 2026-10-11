using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Limita.Application.Common;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Auth.Models;
using Limita.Application.Features.Auth.Services;
using Limita.Domain.Entities;
using Limita.Domain.Enums;

namespace Limita.API.Controllers;

public class AuthController : BaseApiController
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IAuthService _authService;
    private readonly IValidator<LogoutRequest> _logoutValidator;
    private readonly IValidator<ChangePasswordRequestDto> _changePasswordValidator;
    private readonly IValidator<ForgotPasswordRequestDto> _forgotPasswordValidator;
    private readonly IValidator<ResetPasswordRequestDto> _resetPasswordValidator;
    private readonly IHostEnvironment _environment;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IJwtTokenGenerator jwtTokenGenerator,
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IValidator<LoginRequest> loginValidator,
        IValidator<RegisterRequest> registerValidator,
        IAuthService authService,
        IValidator<LogoutRequest> logoutValidator,
        IValidator<ChangePasswordRequestDto> changePasswordValidator,
        IValidator<ForgotPasswordRequestDto> forgotPasswordValidator,
        IValidator<ResetPasswordRequestDto> resetPasswordValidator,
        IHostEnvironment environment)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _context = context;
        _currentUserService = currentUserService;
        _loginValidator = loginValidator;
        _registerValidator = registerValidator;
        _authService = authService;
        _logoutValidator = logoutValidator;
        _changePasswordValidator = changePasswordValidator;
        _forgotPasswordValidator = forgotPasswordValidator;
        _resetPasswordValidator = resetPasswordValidator;
        _environment = environment;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] LoginRequest request)
    {
        var validationResult = await _loginValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return Unauthorized(ApiResponse.Fail("Invalid email or password."));
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return BadRequest(ApiResponse.Fail("Account is locked due to multiple failed login attempts. Please try again later."));
        }

        if (!result.Succeeded)
        {
            return Unauthorized(ApiResponse.Fail("Invalid email or password."));
        }

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user, roles);
        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _userManager.UpdateAsync(user);

        var authResponse = new AuthResponse(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(60),
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            roles);

        return Ok(ApiResponse<AuthResponse>.Ok(authResponse, "Authentication successful."));
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register([FromBody] RegisterRequest request)
    {
        var validationResult = await _registerValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return BadRequest(ApiResponse.Fail("A user with this email address already exists."));
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            return BadRequest(ApiResponse.Fail("Failed to create user.", errors));
        }

        await _userManager.AddToRoleAsync(user, "Customer");

        // Open initial primary account
        var account = new Account
        {
            UserId = user.Id,
            AccountNumber = $"EG{DateTime.UtcNow.Ticks.ToString()[^14..]}",
            Type = AccountType.Checking,
            Currency = "EGP",
            Balance = 1000.00m,
            DailyLimit = 50000.00m,
            MonthlyLimit = 250000.00m,
            Status = AccountStatus.Active
        };

        await _context.Accounts.AddAsync(account);
        await _context.SaveChangesAsync();

        var roles = new List<string> { "Customer" };
        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user, roles);
        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _userManager.UpdateAsync(user);

        var authResponse = new AuthResponse(
            accessToken,
            refreshToken,
            DateTime.UtcNow.AddMinutes(60),
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            roles);

        return Ok(ApiResponse<AuthResponse>.Ok(authResponse, "Registration completed successfully."));
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var principal = _jwtTokenGenerator.GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal == null)
        {
            return BadRequest(ApiResponse.Fail("Invalid access token."));
        }

        var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(ApiResponse.Fail("Invalid token payload."));
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || user.RefreshToken != request.RefreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            return BadRequest(ApiResponse.Fail("Invalid or expired refresh token."));
        }

        var roles = await _userManager.GetRolesAsync(user);
        var newAccessToken = _jwtTokenGenerator.GenerateAccessToken(user, roles);
        var newRefreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _userManager.UpdateAsync(user);

        var authResponse = new AuthResponse(
            newAccessToken,
            newRefreshToken,
            DateTime.UtcNow.AddMinutes(60),
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            roles);

        return Ok(ApiResponse<AuthResponse>.Ok(authResponse, "Token refreshed successfully."));
    }

    /// <summary>
    /// Invalidate and revoke the user's refresh token (logout).
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse>> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _logoutValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var succeeded = await _authService.LogoutAsync(request.RefreshToken, cancellationToken);
        if (!succeeded)
        {
            return BadRequest(ApiResponse.Fail("Invalid or not found refresh token."));
        }

        return Ok(ApiResponse.Ok("Logged out successfully."));
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> GetCurrentUser()
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound(ApiResponse.Fail("User not found."));
        }

        var roles = await _userManager.GetRolesAsync(user);

        var data = new
        {
            user.Id,
            user.FullName,
            user.Email,
            user.PhoneNumber,
            Roles = roles,
            user.CreatedAt
        };

        return Ok(ApiResponse<object>.Ok(data, "Current user profile fetched successfully."));
    }

    /// <summary>
    /// Change password for authenticated user.
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse>> ChangePassword(
        [FromBody] ChangePasswordRequestDto request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _changePasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")?.Value
            ?? _currentUserService.UserId;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var result = await _authService.ChangePasswordAsync(userId, request, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(ApiResponse.Fail(result.Message ?? "Failed to change password.", result.Errors));
        }

        return Ok(ApiResponse.Ok(result.Message ?? "Password changed successfully."));
    }

    /// <summary>
    /// Request password reset token via email.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<string>>> ForgotPassword(
        [FromBody] ForgotPasswordRequestDto request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _forgotPasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var result = await _authService.ForgotPasswordAsync(request, cancellationToken);

        // Security best practice: Never expose reset token in API response; generic message only
        return Ok(ApiResponse<string?>.Ok(null, result.Message ?? "If this email is registered, a password reset link/code has been sent."));
    }

    /// <summary>
    /// Reset password using the received 6-digit OTP verification code.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse>> ResetPassword(
        [FromBody] ResetPasswordRequestDto request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _resetPasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var result = await _authService.ResetPasswordAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(ApiResponse.Fail(result.Message ?? "Failed to reset password.", result.Errors));
        }

        return Ok(ApiResponse.Ok(result.Message ?? "Password has been reset successfully."));
    }
}
