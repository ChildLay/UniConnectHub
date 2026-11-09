using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniConnect.Application.Common;
using UniConnect.Application.Notifications;

namespace UniConnect.Api.Controllers;

[ApiController, Authorize, Route("api/v1/notifications"), Produces("application/json")]
public sealed class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpGet] public Task<PagedResult<NotificationResponse>> List([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => notifications.ListAsync(UserId(), new(pageNumber, pageSize), ct);
    [HttpPost("{id:guid}/read")] public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct) { await notifications.MarkReadAsync(UserId(), id, ct); return NoContent(); }
    [HttpPost("read-all")] public async Task<IActionResult> MarkAllRead(CancellationToken ct) { await notifications.MarkAllReadAsync(UserId(), ct); return NoContent(); }
    private Guid UserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : throw new UnauthorizedAccessException("User identity is missing.");
}
