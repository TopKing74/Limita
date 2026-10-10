using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Limita.Application.Common.Interfaces;
using Limita.Domain.Entities;

namespace Limita.Application.Features.Auth.Services;

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthService(
        IApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
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
}
