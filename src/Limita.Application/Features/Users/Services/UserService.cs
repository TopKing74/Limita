using Microsoft.AspNetCore.Identity;
using Limita.Application.Features.Users.Models;
using Limita.Domain.Entities;

namespace Limita.Application.Features.Users.Services;

public class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<UserProfileDto?> GetProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        var (firstName, lastName) = SplitFullName(user.FullName);

        return new UserProfileDto(
            user.Id,
            user.Email ?? string.Empty,
            firstName,
            lastName,
            user.PhoneNumber);
    }

    public async Task<UserProfileDto?> UpdateProfileAsync(
        string userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();

        user.FullName = $"{firstName} {lastName}".Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? null
            : request.PhoneNumber.Trim();

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        return new UserProfileDto(
            user.Id,
            user.Email ?? string.Empty,
            firstName,
            lastName,
            user.PhoneNumber);
    }

    private static (string FirstName, string LastName) SplitFullName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return (string.Empty, string.Empty);
        }

        var parts = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => (string.Empty, string.Empty),
            1 => (parts[0], string.Empty),
            _ => (parts[0], parts[1])
        };
    }
}
