using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniConnect.Application.Common;
using UniConnect.Application.Opportunities;
using UniConnect.Domain.Opportunities;

namespace UniConnect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/opportunities")]
[Produces("application/json")]
public sealed class OpportunitiesController(IOpportunityService opportunities) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OpportunityResponse>> Create(OpportunityInput request, CancellationToken cancellationToken)
    {
        var item = await opportunities.CreateAsync(UserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }
    [HttpGet]
    public Task<PagedResult<OpportunityResponse>> List([FromQuery] OpportunityType? type, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) => opportunities.ListAsync(UserId(), type, new(pageNumber, pageSize), cancellationToken);
    [HttpGet("{id:guid}")]
    public Task<OpportunityResponse> Get(Guid id, CancellationToken cancellationToken) => opportunities.GetAsync(UserId(), id, cancellationToken);
    [HttpPut("{id:guid}")]
    public Task<OpportunityResponse> Update(Guid id, OpportunityInput request, CancellationToken cancellationToken) => opportunities.UpdateAsync(UserId(), id, request, cancellationToken);
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) { await opportunities.DeleteAsync(UserId(), id, cancellationToken); return NoContent(); }
    [HttpPost("{id:guid}/save")]
    public async Task<IActionResult> Save(Guid id, CancellationToken cancellationToken) { await opportunities.SaveAsync(UserId(), id, cancellationToken); return NoContent(); }
    [HttpDelete("{id:guid}/save")]
    public async Task<IActionResult> Unsave(Guid id, CancellationToken cancellationToken) { await opportunities.UnsaveAsync(UserId(), id, cancellationToken); return NoContent(); }
    [HttpGet("saved")]
    public Task<PagedResult<OpportunityResponse>> Saved([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) => opportunities.SavedAsync(UserId(), new(pageNumber, pageSize), cancellationToken);
    private Guid UserId() { var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"); return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("User identity is missing."); }
}
