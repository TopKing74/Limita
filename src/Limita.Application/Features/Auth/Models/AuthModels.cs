namespace Limita.Application.Features.Auth.Models;

public record LoginRequest(string Email, string Password);

public record RegisterRequest(string FullName, string Email, string PhoneNumber, string Password);

public record RefreshTokenRequest(string AccessToken, string RefreshToken);

public record LogoutRequest(string RefreshToken);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    string UserId,
    string Email,
    string FullName,
    IEnumerable<string> Roles);

public record ChangePasswordRequestDto(
    string CurrentPassword = "",
    string NewPassword = "",
    string ConfirmNewPassword = "");

public record ForgotPasswordRequestDto(
    string Email = "");

public record ResetPasswordRequestDto(
    string Email = "",
    string OtpCode = "",
    string NewPassword = "",
    string ConfirmNewPassword = "");
