namespace Limita.Application.Features.Auth.Services;

public interface IAuthService
{
    Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
}
