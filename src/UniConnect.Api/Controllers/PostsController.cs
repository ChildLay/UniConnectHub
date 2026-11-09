using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniConnect.Application.Common;
using UniConnect.Application.Posts;

namespace UniConnect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/posts")]
[Produces("application/json")]
public sealed class PostsController(IPostService posts) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PostResponse>> Create(CreatePostRequest request, CancellationToken cancellationToken)
    {
        var created = await posts.CreateAsync(UserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }
    [HttpGet]
    public Task<PagedResult<PostResponse>> List([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) => posts.ListAsync(UserId(), new(pageNumber, pageSize), cancellationToken);
    [HttpGet("{id:guid}")]
    public Task<PostResponse> Get(Guid id, CancellationToken cancellationToken) => posts.GetAsync(UserId(), id, cancellationToken);
    [HttpPut("{id:guid}")]
    public Task<PostResponse> Update(Guid id, UpdatePostRequest request, CancellationToken cancellationToken) => posts.UpdateAsync(UserId(), id, request, cancellationToken);
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) { await posts.DeleteAsync(UserId(), id, cancellationToken); return NoContent(); }
    [HttpGet("{id:guid}/comments")]
    public Task<PagedResult<CommentResponse>> ListComments(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        posts.ListCommentsAsync(UserId(), id, new(pageNumber, pageSize), cancellationToken);
    [HttpPost("{id:guid}/comments")]
    public Task<CommentResponse> Comment(Guid id, CreateCommentRequest request, CancellationToken cancellationToken) => posts.AddCommentAsync(UserId(), id, request, cancellationToken);
    [HttpPost("{id:guid}/reactions")]
    public async Task<IActionResult> React(Guid id, SetReactionRequest request, CancellationToken cancellationToken) { await posts.SetReactionAsync(UserId(), id, request.Type, cancellationToken); return NoContent(); }
    [HttpDelete("{id:guid}/reactions")]
    public async Task<IActionResult> Unreact(Guid id, CancellationToken cancellationToken) { await posts.RemoveReactionAsync(UserId(), id, cancellationToken); return NoContent(); }
    [HttpPost("{id:guid}/bookmark")]
    public async Task<IActionResult> Bookmark(Guid id, CancellationToken cancellationToken) { await posts.SetBookmarkAsync(UserId(), id, cancellationToken); return NoContent(); }
    [HttpDelete("{id:guid}/bookmark")]
    public async Task<IActionResult> Unbookmark(Guid id, CancellationToken cancellationToken) { await posts.RemoveBookmarkAsync(UserId(), id, cancellationToken); return NoContent(); }
    [HttpGet("bookmarks")]
    public Task<PagedResult<PostResponse>> Bookmarks([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) => posts.BookmarksAsync(UserId(), new(pageNumber, pageSize), cancellationToken);
    private Guid UserId() { var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"); return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("User identity is missing."); }
}

[ApiController]
[Authorize]
[Route("api/v1/comments")]
public sealed class CommentsController(IPostService posts) : ControllerBase
{
    [HttpPut("{id:guid}")]
    public Task<CommentResponse> Update(Guid id, CreateCommentRequest request, CancellationToken cancellationToken)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(claim, out var userId)) throw new UnauthorizedAccessException("User identity is missing.");
        return posts.UpdateCommentAsync(userId, id, request, cancellationToken);
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(claim, out var userId)) return Unauthorized();
        await posts.DeleteCommentAsync(userId, id, cancellationToken); return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/v1/feed")]
public sealed class FeedController(IPostService posts) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<PostResponse>> Get([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(claim, out var userId)) throw new UnauthorizedAccessException("User identity is missing.");
        return posts.ListAsync(userId, new(pageNumber, pageSize), cancellationToken);
    }
}
