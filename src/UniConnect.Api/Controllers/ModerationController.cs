using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniConnect.Application.Common;
using UniConnect.Application.Moderation;
using UniConnect.Domain.Moderation;

namespace UniConnect.Api.Controllers;

[ApiController, Authorize, Route("api/v1/moderation"), Produces("application/json")]
public sealed class ModerationController(IModerationService moderation) : ControllerBase
{
    [HttpGet("reports"), Authorize(Roles = "Admin,Moderator")]
    public Task<PagedResult<ReportResponse>> List([FromQuery] ReportStatus? status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => moderation.ListAsync(status, new(pageNumber, pageSize), ct);

    [HttpGet("users"), Authorize(Roles = "Admin")]
    [ProducesResponseType<PagedResult<ManagedUserResponse>>(StatusCodes.Status200OK)]
    public Task<PagedResult<ManagedUserResponse>> Users([FromQuery] string? search, [FromQuery] bool? active, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => moderation.ListUsersAsync(search, active, new(pageNumber, pageSize), ct);

    [HttpGet("reports/{id:guid}"), Authorize(Roles = "Admin,Moderator")]
    public Task<ReportResponse> Get(Guid id, CancellationToken ct) => moderation.GetAsync(id, ct);

    [HttpPost("reports/{id:guid}/resolve"), Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> Resolve(Guid id, ResolveReportRequest request, CancellationToken ct) { await moderation.ResolveAsync(UserId(), id, request, ct); return NoContent(); }

    [HttpPost("content/{id:guid}/hide"), Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> Hide(Guid id, [FromQuery] string type, CancellationToken ct) { await moderation.HideAsync(UserId(), type, id, ct); return NoContent(); }

    [HttpPost("users/{id:guid}/suspend"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct) { await moderation.SuspendAsync(UserId(), id, ct); return NoContent(); }

    [HttpPost("users/{id:guid}/role"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetRole(Guid id, [FromBody] UserRole role, CancellationToken ct) { await moderation.SetRoleAsync(UserId(), id, role, ct); return NoContent(); }

    private Guid UserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : throw new UnauthorizedAccessException("User identity is missing.");
}
