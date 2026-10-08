namespace Limita.Application.Features.Auth.Models;

public record LoginRequest(string Email, string Password);

public record RegisterRequest(string FullName, string Email, string PhoneNumber, string Password);

public record RefreshTokenRequest(string AccessToken, string RefreshToken);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    string UserId,
    string Email,
    string FullName,
    IEnumerable<string> Roles);
