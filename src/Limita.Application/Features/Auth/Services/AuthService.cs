using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Limita.Application.Common;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Auth.Models;
using Limita.Domain.Entities;

namespace Limita.Application.Features.Auth.Services;

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AuthService>? _logger;

    public AuthService(
        IApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        IMemoryCache cache,
        ILogger<AuthService>? logger = null)
    {
        _context = context;
        _userManager = userManager;
        _emailService = emailService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return false;
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken, cancellationToken);

        if (user is null)
        {
            return false;
        }

        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<Result<bool>> ChangePasswordAsync(
        string userId,
        ChangePasswordRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<bool>.Failure("User identifier is invalid.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Result<bool>.Failure("User not found.");
        }

        var changeResult = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!changeResult.Succeeded)
        {
            var errors = changeResult.Errors.Select(e => e.Description).ToList();
            return Result<bool>.Failure(errors, "Failed to change password.");
        }

        // Revoke active refresh tokens and update security stamp to invalidate compromised sessions
        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        await _userManager.UpdateSecurityStampAsync(user);
        await _userManager.UpdateAsync(user);

        return Result<bool>.Success(true, "Password changed successfully.");
    }

    public async Task<Result<string>> ForgotPasswordAsync(
        ForgotPasswordRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        const string safeMessage = "If this email is registered, a password reset link/code has been sent.";

        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            return Result<string>.Success(string.Empty, safeMessage);
        }

        var trimmedEmail = dto.Email.Trim();
        var user = await _userManager.FindByEmailAsync(trimmedEmail);
        if (user is null || !user.EmailConfirmed)
        {
            // Return generic success result to prevent account enumeration
            return Result<string>.Success(string.Empty, safeMessage);
        }

        // Generate a cryptographically secure 6-digit numeric OTP
        var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var otp = new PasswordResetOtp
        {
            Email = user.Email!,
            OtpCode = otpCode,
            ExpiryTime = DateTime.UtcNow.AddMinutes(10),
            AttemptsCount = 0,
            IsUsed = false
        };

        // Cache the OTP with a 10-minute expiration
        var cacheKey = $"otp:reset:{user.Email!.Trim().ToLowerInvariant()}";
        _cache.Set(cacheKey, otp, TimeSpan.FromMinutes(10));

        // Safe dev logging (without leaking OTP in public API response)
        _logger?.LogInformation("Password reset OTP generated for user {UserId}", user.Id);

        try
        {
            var htmlBody = BuildPasswordResetEmailTemplate(otpCode);
            await _emailService.SendEmailAsync(
                user.Email!,
                "Limita Bank — Password Reset Request",
                htmlBody,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Suppress error leakage to client to prevent user enumeration / security leaks
            _logger?.LogError(ex, "Failed to dispatch password reset email to {Email}", user.Email);
        }

        return Result<string>.Success(string.Empty, safeMessage);
    }

    private static string BuildPasswordResetEmailTemplate(string otpCode)
    {
        var encodedOtp = System.Net.WebUtility.HtmlEncode(otpCode);

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Limita Bank — Password Reset</title>
</head>
<body style=""margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; background-color: #f4f6f9; color: #1e293b;"">
    <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""background-color: #f4f6f9; padding: 40px 15px;"">
        <tr>
            <td align=""center"">
                <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""max-width: 600px; background-color: #ffffff; border-radius: 12px; box-shadow: 0 4px 20px rgba(0, 0, 0, 0.08); overflow: hidden; border: 1px solid #e2e8f0;"">
                    <!-- Brand Header -->
                    <tr>
                        <td style=""background: linear-gradient(135deg, #0f172a 0%, #1e3a8a 100%); padding: 32px 40px; text-align: center;"">
                            <h1 style=""margin: 0; font-size: 26px; font-weight: 700; color: #ffffff; letter-spacing: 1px;"">
                                LIMITA BANK
                            </h1>
                            <p style=""margin: 6px 0 0; font-size: 13px; color: #93c5fd; text-transform: uppercase; letter-spacing: 1.5px;"">
                                Secure Digital Banking
                            </p>
                        </td>
                    </tr>
                    <!-- Main Content -->
                    <tr>
                        <td style=""padding: 40px 40px 32px;"">
                            <h2 style=""margin: 0 0 16px; font-size: 20px; font-weight: 600; color: #0f172a;"">
                                Password Reset Verification Code
                            </h2>
                            <p style=""margin: 0 0 16px; font-size: 15px; line-height: 1.6; color: #475569;"">
                                Hello,
                            </p>
                            <p style=""margin: 0 0 24px; font-size: 15px; line-height: 1.6; color: #475569;"">
                                We received a request to reset the password for your Limita Bank account. To complete your password reset, please use the 6-digit verification code below:
                            </p>
                            
                            <!-- OTP Box -->
                            <div style=""background-color: #f8fafc; border: 1px solid #cbd5e1; border-radius: 12px; padding: 24px 20px; margin: 24px 0; text-align: center;"">
                                <span style=""display: block; font-size: 12px; font-weight: 700; text-transform: uppercase; letter-spacing: 2px; color: #64748b; margin-bottom: 12px;"">
                                    Your 6-Digit Reset Code
                                </span>
                                <div style=""display: inline-block; font-family: 'Consolas', 'Courier New', monospace; font-size: 34px; font-weight: 800; color: #1e3a8a; letter-spacing: 8px; padding: 10px 24px; background: #ffffff; border: 2px dashed #2563eb; border-radius: 8px;"">
                                    {encodedOtp}
                                </div>
                                <span style=""display: block; font-size: 12px; color: #64748b; margin-top: 12px;"">
                                    Valid for 10 minutes &bull; Do not share this code
                                </span>
                            </div>

                            <p style=""margin: 0 0 16px; font-size: 14px; line-height: 1.6; color: #64748b;"">
                                <strong>Important Security Information:</strong>
                            </p>
                            <ul style=""margin: 0 0 24px; padding-left: 20px; font-size: 14px; line-height: 1.6; color: #64748b;"">
                                <li style=""margin-bottom: 8px;"">This verification code will expire in <strong>10 minutes</strong>.</li>
                                <li style=""margin-bottom: 8px;"">If you did not make this request, you can safely ignore this email. Your password remains unchanged.</li>
                                <li>Limita Bank employees will <strong>never</strong> ask for your password, PIN, or verification OTP.</li>
                            </ul>

                            <p style=""margin: 28px 0 0; font-size: 14px; color: #475569; line-height: 1.5;"">
                                Best regards,<br>
                                <strong>Limita Bank Security Team</strong>
                            </p>
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style=""background-color: #f8fafc; padding: 24px 40px; text-align: center; border-top: 1px solid #e2e8f0;"">
                            <p style=""margin: 0 0 8px; font-size: 12px; color: #94a3b8;"">
                                &copy; 2026 Limita Digital Bank. All rights reserved.
                            </p>
                            <p style=""margin: 0; font-size: 11px; color: #94a3b8; line-height: 1.4;"">
                                This is an automated security transmission. Please do not reply directly to this email.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    public async Task<Result<bool>> ResetPasswordAsync(
        ResetPasswordRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
        {
            return Result<bool>.Failure("Email is required.");
        }

        var trimmedEmail = dto.Email.Trim();
        var user = await _userManager.FindByEmailAsync(trimmedEmail);
        if (user is null)
        {
            return Result<bool>.Failure("Invalid password reset request.");
        }

        var cacheKey = $"otp:reset:{trimmedEmail.ToLowerInvariant()}";
        if (!_cache.TryGetValue(cacheKey, out PasswordResetOtp? otp) || otp is null || otp.IsUsed)
        {
            return Result<bool>.Failure("Invalid or expired password reset OTP.");
        }

        if (DateTime.UtcNow > otp.ExpiryTime)
        {
            _cache.Remove(cacheKey);
            return Result<bool>.Failure("Password reset OTP has expired. Please request a new code.");
        }

        const int maxAttempts = 5;
        if (otp.AttemptsCount >= maxAttempts)
        {
            _cache.Remove(cacheKey);
            return Result<bool>.Failure("Maximum OTP verification attempts exceeded. Please request a new code.");
        }

        if (otp.OtpCode != dto.OtpCode?.Trim())
        {
            otp.AttemptsCount++;
            var remainingTtl = otp.ExpiryTime - DateTime.UtcNow;
            if (remainingTtl > TimeSpan.Zero)
            {
                _cache.Set(cacheKey, otp, remainingTtl);
            }
            else
            {
                _cache.Remove(cacheKey);
            }

            var remaining = maxAttempts - otp.AttemptsCount;
            return Result<bool>.Failure(remaining > 0
                ? $"Invalid OTP code. You have {remaining} attempt(s) remaining."
                : "Invalid OTP code. Maximum attempts reached. Please request a new code.");
        }

        // OTP is valid! Generate Identity token internally
        var identityToken = await _userManager.GeneratePasswordResetTokenAsync(user);

        // Immediately execute reset password
        var resetResult = await _userManager.ResetPasswordAsync(user, identityToken, dto.NewPassword);
        if (!resetResult.Succeeded)
        {
            var errors = resetResult.Errors.Select(e => e.Description).ToList();
            return Result<bool>.Failure(errors, "Failed to reset password.");
        }

        // Invalidate/Remove the used OTP
        otp.IsUsed = true;
        _cache.Remove(cacheKey);

        // Revoke active refresh tokens and update security stamp
        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        await _userManager.UpdateSecurityStampAsync(user);
        await _userManager.UpdateAsync(user);

        return Result<bool>.Success(true, "Password has been reset successfully.");
    }
}
