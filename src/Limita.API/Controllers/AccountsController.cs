using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Limita.Application.Common;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Accounts.Models;
using Limita.Application.Features.Accounts.Services;

namespace Limita.API.Controllers;

[Authorize]
public class AccountsController : BaseApiController
{
    private readonly IAccountService _accountService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<CreateAccountRequest> _createAccountValidator;
    private readonly IValidator<WithdrawalRequest> _withdrawalRequestValidator;
    private readonly IValidator<RechargeRequest> _rechargeRequestValidator;

    public AccountsController(
        IAccountService accountService,
        ICurrentUserService currentUserService,
        IValidator<CreateAccountRequest> createAccountValidator,
        IValidator<WithdrawalRequest> withdrawalRequestValidator,
        IValidator<RechargeRequest> rechargeRequestValidator)
    {
        _accountService = accountService;
        _currentUserService = currentUserService;
        _createAccountValidator = createAccountValidator;
        _withdrawalRequestValidator = withdrawalRequestValidator;
        _rechargeRequestValidator = rechargeRequestValidator;
    }

    /// <summary>
    /// Open a new secondary bank account (Checking / Savings) for the current user.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AccountDetailsDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AccountDetailsDto>>> CreateAccount(
        [FromBody] CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var validationResult = await _createAccountValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var account = await _accountService.CreateSecondaryAccountAsync(userId, request, cancellationToken);

        return CreatedAtAction(
            nameof(GetAccountById),
            new { id = account.Id },
            ApiResponse<AccountDetailsDto>.Ok(account, "Secondary account opened successfully."));
    }

    /// <summary>
    /// Get list of all bank accounts belonging to the current user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AccountSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AccountSummaryDto>>>> GetAccounts(
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var accounts = await _accountService.GetUserAccountsAsync(userId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AccountSummaryDto>>.Ok(accounts, "Accounts retrieved successfully."));
    }

    /// <summary>
    /// Get detailed account overview and status for an owned account.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AccountDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AccountDetailsDto>>> GetAccountById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var account = await _accountService.GetAccountByIdAsync(userId, id, cancellationToken);
        if (account is null)
        {
            return NotFound(ApiResponse.Fail("Account not found or you do not have access to it."));
        }

        return Ok(ApiResponse<AccountDetailsDto>.Ok(account, "Account details retrieved successfully."));
    }

    /// <summary>
    /// Real-time balance inquiry for an owned account.
    /// </summary>
    [HttpGet("{id:guid}/balance")]
    [ProducesResponseType(typeof(ApiResponse<AccountBalanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AccountBalanceDto>>> GetAccountBalance(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var balance = await _accountService.GetAccountBalanceAsync(userId, id, cancellationToken);
        if (balance is null)
        {
            return NotFound(ApiResponse.Fail("Account not found or you do not have access to it."));
        }

        return Ok(ApiResponse<AccountBalanceDto>.Ok(balance, "Account balance retrieved successfully."));
    }

    /// <summary>
    /// Withdraw funds from an owned account.
    /// </summary>
    [HttpPost("{id:guid}/withdraw")]
    [ProducesResponseType(typeof(ApiResponse<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TransactionDto>>> Withdraw(
        Guid id,
        [FromBody] WithdrawalRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var validationResult = await _withdrawalRequestValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var transaction = await _accountService.WithdrawAsync(userId, id, request, cancellationToken);
        return Ok(ApiResponse<TransactionDto>.Ok(transaction, "Withdrawal successful."));
    }

    /// <summary>
    /// Recharge funds to an owned account.
    /// </summary>
    [HttpPost("{id:guid}/recharge")]
    [ProducesResponseType(typeof(ApiResponse<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<TransactionDto>>> Recharge(
        Guid id,
        [FromBody] RechargeRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var validationResult = await _rechargeRequestValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var transaction = await _accountService.RechargeAsync(userId, id, request, cancellationToken);
        return Ok(ApiResponse<TransactionDto>.Ok(transaction, "Recharge successful."));
    }

    private string? GetRequiredUserId() =>
        string.IsNullOrWhiteSpace(_currentUserService.UserId) ? null : _currentUserService.UserId;
}
