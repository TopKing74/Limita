using Limita.Application.Features.Bills.Models;

namespace Limita.Application.Features.Bills.Services;

public interface IBillService
{
    Task<BillDto> CreateBillAsync(string userId, CreateBillRequest request, CancellationToken cancellationToken = default);
    Task<IEnumerable<BillDto>> GetUserBillsAsync(string userId, Guid? providerId = null, CancellationToken cancellationToken = default);
    Task<BillPaymentResponseDto> PayBillAsync(string userId, Guid billId, PayBillRequest request, CancellationToken cancellationToken = default);
}
