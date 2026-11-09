using Microsoft.AspNetCore.Mvc;

namespace UniConnect.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public sealed class VersionController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiVersionOverview>(StatusCodes.Status200OK)]
    public ActionResult<ApiVersionOverview> GetVersion() => Ok(new ApiVersionOverview(
        "UniConnect Hub", "v1", "Running",
        ["auth", "system", "universities", "profiles", "discovery", "connections", "communities", "posts", "comments", "feed", "events", "mentoring", "opportunities", "notifications", "reports", "moderation"],
        []));
}

public sealed record ApiVersionOverview(string Application, string ApiVersion, string Status, IReadOnlyList<string> AvailableModules, IReadOnlyList<string> PlannedModules);
