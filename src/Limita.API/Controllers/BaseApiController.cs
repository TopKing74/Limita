using Microsoft.AspNetCore.Mvc;
using Limita.Application.Common;

namespace Limita.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    protected ActionResult<ApiResponse<T>> HandleResult<T>(T? data, string message = "Success")
    {
        if (data == null)
        {
            return NotFound(ApiResponse<T>.Fail("Requested resource was not found."));
        }

        return Ok(ApiResponse<T>.Ok(data, message));
    }

    protected ActionResult<ApiResponse> HandleSuccess(string message = "Operation completed successfully.")
    {
        return Ok(ApiResponse.Ok(message));
    }

    protected ActionResult<ApiResponse> HandleFailure(string message, List<string>? errors = null, int statusCode = StatusCodes.Status400BadRequest)
    {
        return StatusCode(statusCode, ApiResponse.Fail(message, errors));
    }
}
