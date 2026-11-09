using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniConnect.Application.Common;
using UniConnect.Application.Connections;

namespace UniConnect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/connections")]
[Produces("application/json")]
public sealed class ConnectionsController(IConnectionService connections) : ControllerBase
{
    [HttpPost("requests")]
    [ProducesResponseType<ConnectionRequestResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ConnectionRequestResponse>> Send(ConnectionRequestInput request, CancellationToken cancellationToken)
    {
        var result = await connections.SendAsync(UserId(), request.RecipientId, cancellationToken);
        return Created($"/api/v1/connections/requests/{result.Id}", result);
    }

    [HttpGet("requests/incoming")]
    public Task<PagedResult<ConnectionRequestResponse>> Incoming([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) => connections.ListRequestsAsync(UserId(), true, new(pageNumber, pageSize), cancellationToken);

    [HttpGet("requests/outgoing")]
    public Task<PagedResult<ConnectionRequestResponse>> Outgoing([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) => connections.ListRequestsAsync(UserId(), false, new(pageNumber, pageSize), cancellationToken);

    [HttpPost("requests/{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken cancellationToken) { await connections.AcceptAsync(UserId(), id, cancellationToken); return NoContent(); }

    [HttpPost("requests/{id:guid}/decline")]
    public async Task<IActionResult> Decline(Guid id, CancellationToken cancellationToken) { await connections.DeclineAsync(UserId(), id, cancellationToken); return NoContent(); }

    [HttpDelete("requests/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> CancelRequest(Guid id, CancellationToken cancellationToken)
    {
        await connections.CancelRequestAsync(UserId(), id, cancellationToken);
        return NoContent();
    }
    [HttpGet]
    public Task<PagedResult<ConnectionResponse>> List([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) => connections.ListConnectionsAsync(UserId(), new(pageNumber, pageSize), cancellationToken);

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Remove(Guid userId, CancellationToken cancellationToken) { await connections.RemoveAsync(UserId(), userId, cancellationToken); return NoContent(); }

    [HttpPost("block/{userId:guid}")]
    public async Task<IActionResult> Block(Guid userId, CancellationToken cancellationToken) { await connections.BlockAsync(UserId(), userId, cancellationToken); return NoContent(); }

    [HttpDelete("block/{userId:guid}")]
    public async Task<IActionResult> Unblock(Guid userId, CancellationToken cancellationToken) { await connections.UnblockAsync(UserId(), userId, cancellationToken); return NoContent(); }

    [HttpGet("blocked")]
    public Task<PagedResult<Guid>> Blocked([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) => connections.ListBlockedAsync(UserId(), new(pageNumber, pageSize), cancellationToken);

    private Guid UserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("User identity is missing.");
    }
}

public sealed record ConnectionRequestInput(Guid RecipientId);
