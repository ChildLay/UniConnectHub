using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniConnect.Application.Common;
using UniConnect.Application.Discovery;
using UniConnect.Domain.Users;

namespace UniConnect.Api.Controllers;

[ApiController, Authorize, Route("api/v1/discovery"), Produces("application/json")]
public sealed class DiscoveryController(IDiscoveryService discovery) : ControllerBase
{
    [HttpGet("people")]
    public Task<PagedResult<DiscoveredPerson>> People([FromQuery] Guid? universityId, [FromQuery] string? programme,
        [FromQuery] string? studyArea, [FromQuery] string? skill, [FromQuery] string? interest, [FromQuery] string? industry,
        [FromQuery] string? careerGoal, [FromQuery] JourneyStage? journeyStage, [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        discovery.SearchAsync(UserId(), new(universityId, programme, studyArea, skill, interest, industry, careerGoal, journeyStage, pageNumber, pageSize), ct);
    private Guid UserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : throw new UnauthorizedAccessException("User identity is missing.");
}
