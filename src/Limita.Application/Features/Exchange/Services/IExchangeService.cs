using Limita.Application.Common;
using Limita.Application.Features.Exchange.Models;

namespace Limita.Application.Features.Exchange.Services;

public interface IExchangeService
{


    Task<ApiResponse<ExchangeQuoteDto>> GetQuoteAsync(
        string userId,
        ExchangeQuoteRequest request,
        CancellationToken cancellationToken = default);

   
    Task<ApiResponse<ExchangeResultDto>> ExecuteExchangeAsync(
        string userId,
        ExecuteExchangeRequest request,
        CancellationToken cancellationToken = default);
}
