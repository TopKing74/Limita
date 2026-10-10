using Limita.Application.Features.Users.Models;

namespace Limita.Application.Features.Users.Services;

public interface IUserService
{
    Task<UserProfileDto?> GetProfileAsync(string userId, CancellationToken cancellationToken = default);
    Task<UserProfileDto?> UpdateProfileAsync(string userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);
}
