using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniConnect.Application.Common;
using UniConnect.Application.Mentoring;
using UniConnect.Domain.Mentoring;

namespace UniConnect.Api.Controllers;

[ApiController, Authorize, Route("api/v1/mentoring"), Produces("application/json")]
public sealed class MentoringController(IMentoringService mentoring) : ControllerBase
{
    [HttpPut("profile")] public Task<MentorResponse> SetProfile(MentorProfileRequest request, CancellationToken ct) => mentoring.SetProfileAsync(UserId(), request, ct);
    [HttpGet("mentors")] public Task<PagedResult<MentorResponse>> ListMentors([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => mentoring.ListMentorsAsync(UserId(), new(pageNumber, pageSize), ct);
    [HttpGet("mentors/{userId:guid}")] public Task<MentorResponse> GetMentor(Guid userId, CancellationToken ct) => mentoring.GetMentorAsync(userId, ct);
    [HttpPost("requests")] public async Task<ActionResult<MentorshipResponse>> CreateRequest(MentorshipRequestInput request, CancellationToken ct) { var created = await mentoring.RequestAsync(UserId(), request, ct); return CreatedAtAction(nameof(ListRequests), created); }
    [HttpGet("requests")] public Task<PagedResult<MentorshipResponse>> ListRequests([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => mentoring.ListRequestsAsync(UserId(), new(pageNumber, pageSize), ct);
    [HttpPost("requests/{id:guid}/accept")] public async Task<IActionResult> Accept(Guid id, CancellationToken ct) { await mentoring.TransitionAsync(UserId(), id, MentorshipStatus.Accepted, ct); return NoContent(); }
    [HttpPost("requests/{id:guid}/decline")] public async Task<IActionResult> Decline(Guid id, CancellationToken ct) { await mentoring.TransitionAsync(UserId(), id, MentorshipStatus.Declined, ct); return NoContent(); }
    [HttpGet("relationships")] public Task<PagedResult<MentorshipResponse>> Relationships([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => mentoring.RelationshipsAsync(UserId(), new(pageNumber, pageSize), ct);
    [HttpPost("{id:guid}/complete")] public async Task<IActionResult> Complete(Guid id, CancellationToken ct) { await mentoring.TransitionAsync(UserId(), id, MentorshipStatus.Completed, ct); return NoContent(); }
    [HttpPost("{id:guid}/cancel")] public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) { await mentoring.TransitionAsync(UserId(), id, MentorshipStatus.Cancelled, ct); return NoContent(); }
    private Guid UserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : throw new UnauthorizedAccessException("User identity is missing.");
}
