using Microsoft.EntityFrameworkCore;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.BillProviders.Models;
using Limita.Domain.Entities;

namespace Limita.Application.Features.BillProviders.Services;

public class BillProviderService : IBillProviderService
{
    private readonly IApplicationDbContext _context;

    public BillProviderService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BillProviderDto> CreateBillProviderAsync(
        CreateBillProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.BillProviders
            .AnyAsync(p => p.Name == request.Name, cancellationToken);
            
        if (existing)
        {
            throw new InvalidOperationException($"A Bill Provider with the name '{request.Name}' already exists.");
        }

        var provider = new BillProvider
        {
            Name = request.Name,
            Category = request.Category,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        await _context.BillProviders.AddAsync(provider, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(provider);
    }

    public async Task<IReadOnlyList<BillProviderDto>> GetAllBillProvidersAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.BillProviders
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new BillProviderDto(
                p.Id,
                p.Name,
                p.Category,
                p.IsActive,
                p.CreatedAt,
                p.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<BillProviderDto?> GetBillProviderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var provider = await _context.BillProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return provider is null ? null : MapToDto(provider);
    }

    private static BillProviderDto MapToDto(BillProvider provider) =>
        new(
            provider.Id,
            provider.Name,
            provider.Category,
            provider.IsActive,
            provider.CreatedAt,
            provider.UpdatedAt);
}
