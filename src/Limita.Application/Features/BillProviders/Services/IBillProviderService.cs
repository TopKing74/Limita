using Limita.Application.Features.BillProviders.Models;

namespace Limita.Application.Features.BillProviders.Services;

public interface IBillProviderService
{
    Task<BillProviderDto> CreateBillProviderAsync(
        CreateBillProviderRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BillProviderDto>> GetAllBillProvidersAsync(
        CancellationToken cancellationToken = default);

    Task<BillProviderDto?> GetBillProviderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
