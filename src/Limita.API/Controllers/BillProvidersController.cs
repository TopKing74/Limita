using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Limita.Application.Features.BillProviders.Models;
using Limita.Application.Features.BillProviders.Services;

namespace Limita.API.Controllers;

[Authorize]
public class BillProvidersController : BaseApiController
{
    private readonly IBillProviderService _billProviderService;

    public BillProvidersController(IBillProviderService billProviderService)
    {
        _billProviderService = billProviderService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BillProviderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var providers = await _billProviderService.GetAllBillProvidersAsync(cancellationToken);
        return Ok(providers);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BillProviderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var provider = await _billProviderService.GetBillProviderByIdAsync(id, cancellationToken);
        if (provider == null)
        {
            return NotFound();
        }
        return Ok(provider);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(BillProviderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateBillProviderRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var provider = await _billProviderService.CreateBillProviderAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = provider.Id }, provider);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }
}
