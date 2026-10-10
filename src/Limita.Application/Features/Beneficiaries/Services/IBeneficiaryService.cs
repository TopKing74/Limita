using Limita.Application.Features.Beneficiaries.Models;

namespace Limita.Application.Features.Beneficiaries.Services;

/// <summary>
/// Service contract defining operations for managing user-specific transfer beneficiaries (saved contacts).
/// </summary>
public interface IBeneficiaryService
{
    /// <summary>
    /// Retrieves all beneficiaries saved by the specified authenticated user.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="cancellationToken">Cancellation token for asynchronous execution.</param>
    /// <returns>A read-only list of beneficiaries sorted with favorites first, then by creation date.</returns>
    Task<IReadOnlyList<BeneficiaryDto>> GetUserBeneficiariesAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single beneficiary by ID, validating that it belongs to the authenticated user.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="beneficiaryId">The unique ID of the beneficiary.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The beneficiary details if found and owned by the user; otherwise, null.</returns>
    Task<BeneficiaryDto?> GetBeneficiaryByIdAsync(
        string userId,
        Guid beneficiaryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a new recipient to the authenticated user's contact directory.
    /// Validates that the account number is not already saved for this user.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="request">The beneficiary creation payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created beneficiary DTO.</returns>
    Task<BeneficiaryDto> CreateBeneficiaryAsync(
        string userId,
        CreateBeneficiaryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates details of an existing beneficiary owned by the authenticated user.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="beneficiaryId">The unique ID of the beneficiary to update.</param>
    /// <param name="request">The update payload containing modified fields.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated beneficiary DTO if found and owned by the user; otherwise, null.</returns>
    Task<BeneficiaryDto?> UpdateBeneficiaryAsync(
        string userId,
        Guid beneficiaryId,
        UpdateBeneficiaryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a saved beneficiary from the authenticated user's directory.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="beneficiaryId">The unique ID of the beneficiary to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the beneficiary was found and deleted; otherwise, false.</returns>
    Task<bool> DeleteBeneficiaryAsync(
        string userId,
        Guid beneficiaryId,
        CancellationToken cancellationToken = default);
}
