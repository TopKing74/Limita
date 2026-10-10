using Limita.Application.Features.Accounts.Models;

namespace Limita.Application.Features.Accounts.Services;

public interface IAccountService
{
    Task<AccountDetailsDto> CreateSecondaryAccountAsync(
        string userId,
        CreateAccountRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountSummaryDto>> GetUserAccountsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<AccountDetailsDto?> GetAccountByIdAsync(
        string userId,
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<AccountBalanceDto?> GetAccountBalanceAsync(
        string userId,
        Guid accountId,
        CancellationToken cancellationToken = default);

    Task<TransactionDto> WithdrawAsync(
        string userId,
        Guid accountId,
        WithdrawalRequest request,
        CancellationToken cancellationToken = default);

    Task<TransactionDto> DepositAsync(
        string userId,
        Guid accountId,
        DepositRequest request,
        CancellationToken cancellationToken = default);

    Task<TransactionDto> RechargeAsync(
        string userId,
        Guid accountId,
        RechargeRequest request,
        CancellationToken cancellationToken = default);
}
