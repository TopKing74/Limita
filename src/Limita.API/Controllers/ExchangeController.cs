using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Limita.Application.Common;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Exchange.Models;
using Limita.Application.Features.Exchange.Services;

namespace Limita.API.Controllers;

/// <summary>
/// Currency exchange quote and execution endpoints.
/// </summary>
[Authorize]
public class ExchangeController : BaseApiController
{
    private readonly IExchangeService _exchangeService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<ExchangeQuoteRequest> _quoteValidator;
    private readonly IValidator<ExecuteExchangeRequest> _executeValidator;

    public ExchangeController(
        IExchangeService exchangeService,
        ICurrentUserService currentUserService,
        IValidator<ExchangeQuoteRequest> quoteValidator,
        IValidator<ExecuteExchangeRequest> executeValidator)
    {
        _exchangeService = exchangeService;
        _currentUserService = currentUserService;
        _quoteValidator = quoteValidator;
        _executeValidator = executeValidator;
    }

    /// <summary>
    /// Calculate an FX quote using the server-side exchange rate table.
    /// Does not move funds.
    /// </summary>
    [HttpPost("quote")]
    [ProducesResponseType(typeof(ApiResponse<ExchangeQuoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<ExchangeQuoteDto>>> GetQuote(
        [FromBody] ExchangeQuoteRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var validationResult = await _quoteValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var result = await _exchangeService.GetQuoteAsync(userId, request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Atomically execute a currency exchange between two owned accounts.
    /// Debits the source, credits the destination at the server-side rate, and records ledger transactions.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ExchangeResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<ExchangeResultDto>>> ExecuteExchange(
        [FromBody] ExecuteExchangeRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var validationResult = await _executeValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var result = await _exchangeService.ExecuteExchangeAsync(userId, request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    private string? GetRequiredUserId() =>
        string.IsNullOrWhiteSpace(_currentUserService.UserId) ? null : _currentUserService.UserId;
}
