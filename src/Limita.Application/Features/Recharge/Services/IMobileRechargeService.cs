using Limita.Application.Features.Recharge.Models;

namespace Limita.Application.Features.Recharge.Services;

public interface IMobileRechargeService
{
    Task<MobileRechargeResponseDto> RechargeMobileAsync(
        string userId,
        MobileRechargeRequest request,
        CancellationToken cancellationToken = default);
}
