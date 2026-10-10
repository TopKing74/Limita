namespace Limita.Application.Features.Users.Models;

public record UserProfileDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber);

public record UpdateProfileRequest(
    string FirstName,
    string LastName,
    string? PhoneNumber);
