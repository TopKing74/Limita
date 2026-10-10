using Microsoft.EntityFrameworkCore;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Bills.Models;
using Limita.Domain.Entities;
using Limita.Domain.Enums;

namespace Limita.Application.Features.Bills.Services;

public class BillService : IBillService
{
    private readonly IApplicationDbContext _context;

    public BillService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BillDto> CreateBillAsync(string userId, CreateBillRequest request, CancellationToken cancellationToken = default)
    {
        // Verify provider exists and is active
        var provider = await _context.BillProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProviderId, cancellationToken);

        if (provider == null)
            throw new InvalidOperationException("The specified Bill Provider does not exist.");

        if (!provider.IsActive)
            throw new InvalidOperationException("The specified Bill Provider is currently inactive.");

        var bill = new Bill
        {
            UserId = userId,
            ProviderId = request.ProviderId,
            BillingNumber = request.BillingNumber,
            Amount = request.Amount,
            DueDate = request.DueDate,
            Status = BillStatus.Pending
        };

        _context.Bills.Add(bill);
        await _context.SaveChangesAsync(cancellationToken);

        return new BillDto(
            bill.Id,
            bill.ProviderId,
            provider.Name,
            bill.BillingNumber,
            bill.Amount,
            bill.DueDate,
            bill.Status,
            bill.CreatedAt
        );
    }

    public async Task<IEnumerable<BillDto>> GetUserBillsAsync(string userId, Guid? providerId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Bills
            .AsNoTracking()
            .Include(b => b.Provider)
            .Where(b => b.UserId == userId);

        if (providerId.HasValue)
        {
            query = query.Where(b => b.ProviderId == providerId.Value);
        }

        var bills = await query.OrderByDescending(b => b.CreatedAt).ToListAsync(cancellationToken);

        return bills.Select(b => new BillDto(
            b.Id,
            b.ProviderId,
            b.Provider?.Name ?? string.Empty,
            b.BillingNumber,
            b.Amount,
            b.DueDate,
            b.Status,
            b.CreatedAt
        ));
    }
}
