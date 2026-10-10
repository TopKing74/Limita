using Microsoft.EntityFrameworkCore;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Beneficiaries.Models;
using Limita.Domain.Entities;

namespace Limita.Application.Features.Beneficiaries.Services;

/// <summary>
/// Implementation of IBeneficiaryService handling business logic, persistence operations,
/// and ownership enforcement for transfer beneficiaries.
/// </summary>
public class BeneficiaryService : IBeneficiaryService
{
    private readonly IApplicationDbContext _context;

    public BeneficiaryService(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BeneficiaryDto>> GetUserBeneficiariesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Beneficiaries
            .AsNoTracking()
            .Where(b => b.OwnerUserId == userId)
            .OrderByDescending(b => b.IsFavorite)
            .ThenByDescending(b => b.CreatedAt)
            .Select(b => new BeneficiaryDto(
                b.Id,
                b.FullName,
                b.AccountNumber,
                b.BankName,
                string.IsNullOrEmpty(b.PhoneNumber) ? null : b.PhoneNumber,
                b.IsFavorite,
                b.CreatedAt,
                b.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BeneficiaryDto?> GetBeneficiaryByIdAsync(
        string userId,
        Guid beneficiaryId,
        CancellationToken cancellationToken = default)
    {
        var beneficiary = await _context.Beneficiaries
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == beneficiaryId && b.OwnerUserId == userId, cancellationToken);

        return beneficiary is null ? null : MapToDto(beneficiary);
    }

    /// <inheritdoc />
    public async Task<BeneficiaryDto> CreateBeneficiaryAsync(
        string userId,
        CreateBeneficiaryRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedAccountNumber = request.AccountNumber.Trim().ToUpperInvariant();

        // Check for duplicate account number under the same authenticated user account
        var alreadyExists = await _context.Beneficiaries
            .AnyAsync(b => b.OwnerUserId == userId && b.AccountNumber == normalizedAccountNumber, cancellationToken);

        if (alreadyExists)
        {
            throw new InvalidOperationException(
                $"A beneficiary with account number '{normalizedAccountNumber}' is already saved in your directory.");
        }

        var beneficiary = new Beneficiary
        {
            OwnerUserId = userId,
            FullName = request.FullName.Trim(),
            AccountNumber = normalizedAccountNumber,
            BankName = request.BankName.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim() ?? string.Empty,
            IsFavorite = request.IsFavorite,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Beneficiaries.AddAsync(beneficiary, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(beneficiary);
    }

    /// <inheritdoc />
    public async Task<BeneficiaryDto?> UpdateBeneficiaryAsync(
        string userId,
        Guid beneficiaryId,
        UpdateBeneficiaryRequest request,
        CancellationToken cancellationToken = default)
    {
        var beneficiary = await _context.Beneficiaries
            .FirstOrDefaultAsync(b => b.Id == beneficiaryId && b.OwnerUserId == userId, cancellationToken);

        if (beneficiary is null)
        {
            return null;
        }

        beneficiary.FullName = request.FullName.Trim();
        beneficiary.BankName = request.BankName.Trim();
        beneficiary.PhoneNumber = request.PhoneNumber?.Trim() ?? string.Empty;
        beneficiary.IsFavorite = request.IsFavorite;
        beneficiary.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(beneficiary);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteBeneficiaryAsync(
        string userId,
        Guid beneficiaryId,
        CancellationToken cancellationToken = default)
    {
        var beneficiary = await _context.Beneficiaries
            .FirstOrDefaultAsync(b => b.Id == beneficiaryId && b.OwnerUserId == userId, cancellationToken);

        if (beneficiary is null)
        {
            return false;
        }

        _context.Beneficiaries.Remove(beneficiary);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static BeneficiaryDto MapToDto(Beneficiary b) =>
        new(
            b.Id,
            b.FullName,
            b.AccountNumber,
            b.BankName,
            string.IsNullOrEmpty(b.PhoneNumber) ? null : b.PhoneNumber,
            b.IsFavorite,
            b.CreatedAt,
            b.UpdatedAt);
}
