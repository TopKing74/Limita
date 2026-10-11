using Limita.Application.Common;
using Limita.Application.Features.Auth.Models;

namespace Limita.Application.Features.Auth.Services;

public interface IAuthService
{
    Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task<Result<bool>> ChangePasswordAsync(string userId, ChangePasswordRequestDto dto, CancellationToken cancellationToken = default);

    Task<Result<string>> ForgotPasswordAsync(ForgotPasswordRequestDto dto, CancellationToken cancellationToken = default);

    Task<Result<bool>> ResetPasswordAsync(ResetPasswordRequestDto dto, CancellationToken cancellationToken = default);
}
