using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniConnect.Application.Moderation;

namespace UniConnect.Api.Controllers;

[ApiController, Authorize, Route("api/v1/reports"), Produces("application/json")]
public sealed class ReportsController(IReportService reports) : ControllerBase
{
    [HttpPost] public async Task<ActionResult<ReportResponse>> Create(CreateReportRequest request, CancellationToken ct) { var result = await reports.CreateAsync(UserId(), request, ct); return Created($"/api/v1/moderation/reports/{result.Id}", result); }
    private Guid UserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : throw new UnauthorizedAccessException("User identity is missing.");
}
