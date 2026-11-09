using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniConnect.Application.Common;
using UniConnect.Application.Events;

namespace UniConnect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/events")]
[Produces("application/json")]
public sealed class EventsController(IEventService events) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<EventResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<EventResponse>> Create(CreateEventRequest request, CancellationToken cancellationToken)
    {
        var created = await events.CreateAsync(UserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet]
    public Task<PagedResult<EventResponse>> List([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        events.ListAsync(UserId(), fromUtc, toUtc, new(pageNumber, pageSize), cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<EventResponse> Get(Guid id, CancellationToken cancellationToken) => events.GetAsync(UserId(), id, cancellationToken);

    [HttpPut("{id:guid}")]
    public Task<EventResponse> Update(Guid id, UpdateEventRequest request, CancellationToken cancellationToken) => events.UpdateAsync(UserId(), id, request, cancellationToken);

    [HttpPost("{id:guid}/register")]
    public async Task<IActionResult> Register(Guid id, CancellationToken cancellationToken) { await events.RegisterAsync(UserId(), id, cancellationToken); return NoContent(); }

    [HttpDelete("{id:guid}/register")]
    public async Task<IActionResult> Unregister(Guid id, CancellationToken cancellationToken) { await events.UnregisterAsync(UserId(), id, cancellationToken); return NoContent(); }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken) { await events.CancelAsync(UserId(), id, cancellationToken); return NoContent(); }

    [HttpGet("{id:guid}/attendees")]
    public Task<PagedResult<Guid>> Attendees(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) => events.AttendeesAsync(UserId(), id, new(pageNumber, pageSize), cancellationToken);

    private Guid UserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("User identity is missing.");
    }
}
