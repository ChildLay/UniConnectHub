using Microsoft.AspNetCore.Mvc;

namespace UniConnect.Api.Controllers;

[ApiController]
[Route("api/v1/system")]
[Produces("application/json")]
public sealed class SystemController : ControllerBase
{
    /// <summary>Returns basic service and API version information.</summary>
    /// <param name="cancellationToken">Request cancellation token.</param>
    [HttpGet("info")]
    [ProducesResponseType<SystemInfoResponse>(StatusCodes.Status200OK)]
    public ActionResult<SystemInfoResponse> GetInfo()
    {
        return Ok(new SystemInfoResponse("UniConnect Hub", "v1", "Running"));
    }
}

/// <summary>Public, non-sensitive information about the running API.</summary>
public sealed record SystemInfoResponse(string Application, string ApiVersion, string Status);
