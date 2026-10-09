using Limita.Domain.Enums;

namespace Limita.Application.Features.BillProviders.Models;

public record BillProviderDto(
    Guid Id,
    string Name,
    BillCategory Category,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CreateBillProviderRequest(
    string Name,
    BillCategory Category,
    bool IsActive = true);
