using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniConnect.Application.Common;
using UniConnect.Application.Communities;

namespace UniConnect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/communities")]
[Produces("application/json")]
public sealed class CommunitiesController(ICommunityService communities) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CommunityResponse>> Create(CommunityInput request, CancellationToken cancellationToken)
    {
        var created = await communities.CreateAsync(UserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet]
    public Task<PagedResult<CommunityResponse>> List([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) => communities.ListAsync(UserId(), new(pageNumber, pageSize), cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<CommunityResponse> Get(Guid id, CancellationToken cancellationToken) => communities.GetAsync(UserId(), id, cancellationToken);

    [HttpPut("{id:guid}")]
    public Task<CommunityResponse> Update(Guid id, CommunityInput request, CancellationToken cancellationToken) => communities.UpdateAsync(UserId(), id, request, cancellationToken);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) { await communities.DeleteAsync(UserId(), id, cancellationToken); return NoContent(); }

    [HttpPost("{id:guid}/join")]
    public async Task<IActionResult> Join(Guid id, CancellationToken cancellationToken) { await communities.JoinAsync(UserId(), id, cancellationToken); return NoContent(); }

    [HttpPost("{id:guid}/leave")]
    public async Task<IActionResult> Leave(Guid id, CancellationToken cancellationToken) { await communities.LeaveAsync(UserId(), id, cancellationToken); return NoContent(); }

    [HttpGet("{id:guid}/members")]
    public Task<PagedResult<Guid>> Members(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) => communities.MembersAsync(UserId(), id, new(pageNumber, pageSize), cancellationToken);

    private Guid UserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("User identity is missing.");
    }
}
