using System.Security.Claims;
using Limita.Domain.Entities;

namespace Limita.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
