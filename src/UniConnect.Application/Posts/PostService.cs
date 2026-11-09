using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Common;
using UniConnect.Domain.Communities;
using UniConnect.Domain.Connections;
using UniConnect.Domain.Content;
using UniConnect.Domain.Profiles;
using UniConnect.Domain.Users;

namespace UniConnect.Application.Posts;

public sealed record PostResponse(Guid Id, Guid AuthorId, string AuthorName, string Content, PostAudience Audience,
    Guid? CommunityId, bool AllowComments, DateTime CreatedAtUtc, int CommentCount, int ReactionCount);
public sealed record CreatePostRequest(string Content, PostAudience Audience, Guid? CommunityId, bool AllowComments, IReadOnlyCollection<Guid>? SelectedUserIds);
public sealed record UpdatePostRequest(string Content, bool AllowComments);
public sealed record CommentResponse(Guid Id, Guid PostId, Guid AuthorId, string Content, DateTime CreatedAtUtc, string AuthorName, string? AuthorProfileImageUrl);
public sealed record CreateCommentRequest(string Content);
public sealed record SetReactionRequest(ReactionType Type);

public interface IPostService
{
    Task<PostResponse> CreateAsync(Guid userId, CreatePostRequest request, CancellationToken cancellationToken);
    Task<PagedResult<PostResponse>> ListAsync(Guid userId, PagedRequest page, CancellationToken cancellationToken);
    Task<PostResponse> GetAsync(Guid userId, Guid postId, CancellationToken cancellationToken);
    Task<PagedResult<CommentResponse>> ListCommentsAsync(Guid userId, Guid postId, PagedRequest page, CancellationToken cancellationToken);
    Task<PostResponse> UpdateAsync(Guid userId, Guid postId, UpdatePostRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid userId, Guid postId, CancellationToken cancellationToken);
    Task<CommentResponse> AddCommentAsync(Guid userId, Guid postId, CreateCommentRequest request, CancellationToken cancellationToken);
    Task DeleteCommentAsync(Guid userId, Guid commentId, CancellationToken cancellationToken);
    Task<CommentResponse> UpdateCommentAsync(Guid userId, Guid commentId, CreateCommentRequest request, CancellationToken cancellationToken);
    Task SetReactionAsync(Guid userId, Guid postId, ReactionType type, CancellationToken cancellationToken);
    Task RemoveReactionAsync(Guid userId, Guid postId, CancellationToken cancellationToken);
    Task SetBookmarkAsync(Guid userId, Guid postId, CancellationToken cancellationToken);
    Task RemoveBookmarkAsync(Guid userId, Guid postId, CancellationToken cancellationToken);
    Task<PagedResult<PostResponse>> BookmarksAsync(Guid userId, PagedRequest page, CancellationToken cancellationToken);
}

public sealed class PostService(IApplicationDbContext db, IClock clock) : IPostService
{
    public async Task<PostResponse> CreateAsync(Guid userId, CreatePostRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Audience)) throw new ArgumentException("Post audience is invalid.");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken) ?? throw new UnauthorizedAccessException();
        if (request.Audience == PostAudience.CommunityMembers)
        {
            if (request.CommunityId is null || !await db.CommunityMembers.AnyAsync(x => x.CommunityId == request.CommunityId && x.UserId == userId, cancellationToken))
                throw new AccessDeniedException("You must be a member of the selected community to post there.");
        }
        var selected = request.SelectedUserIds?.Distinct().ToArray() ?? [];
        if (request.Audience == PostAudience.SelectedUsers && (selected.Length == 0 || selected.Length > 100)) throw new ArgumentException("Selected-user audiences require between 1 and 100 recipients.");
        var post = new Post(userId, user.UniversityId, request.Content, request.Audience, clock.UtcNow, request.CommunityId, request.AllowComments);
        await db.Posts.AddAsync(post, cancellationToken);
        foreach (var targetId in selected) await db.PostAudienceUsers.AddAsync(new PostAudienceUser(post.Id, targetId), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, post.Id, cancellationToken);
    }

    public async Task<PagedResult<PostResponse>> ListAsync(Guid userId, PagedRequest page, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken) ?? throw new UnauthorizedAccessException();
        var blockedBy = db.BlockedUsers.Where(b => b.BlockerId == userId).Select(b => b.BlockedId);
        var blockedMe = db.BlockedUsers.Where(b => b.BlockedId == userId).Select(b => b.BlockerId);
        var query = db.Posts.AsNoTracking().Where(p => !p.IsDeleted && !p.IsHidden &&
            !blockedBy.Contains(p.AuthorId) && !blockedMe.Contains(p.AuthorId) &&
            (p.AuthorId == userId ||
             p.Audience == PostAudience.UniversityCommunity && p.UniversityId == user.UniversityId ||
             p.Audience == PostAudience.StudentsOnly && p.UniversityId == user.UniversityId && (user.JourneyStage == JourneyStage.NewStudent || user.JourneyStage == JourneyStage.CurrentStudent) ||
             p.Audience == PostAudience.GraduatesAndAlumni && p.UniversityId == user.UniversityId && (user.JourneyStage == JourneyStage.Graduate || user.JourneyStage == JourneyStage.Alumni) ||
             p.Audience == PostAudience.ConnectionsOnly && db.Connections.Any(c => (c.UserId == userId && c.ConnectedUserId == p.AuthorId) || (c.UserId == p.AuthorId && c.ConnectedUserId == userId)) ||
             p.Audience == PostAudience.CommunityMembers && p.CommunityId != null && db.CommunityMembers.Any(m => m.CommunityId == p.CommunityId && m.UserId == userId) ||
             p.Audience == PostAudience.SelectedUsers && db.PostAudienceUsers.Any(a => a.PostId == p.Id && a.UserId == userId)));
        var total = await query.LongCountAsync(cancellationToken);
        var posts = await query.OrderByDescending(p => p.CreatedAtUtc).Skip(page.Skip).Take(page.SafePageSize).ToListAsync(cancellationToken);
        // Connection-only visibility is removed after page retrieval until the candidate set is narrowed in a separate query.
        var allowed = new List<Post>();
        foreach (var post in posts)
        {
            if (post.Audience != PostAudience.ConnectionsOnly || post.AuthorId == userId || await AreConnected(userId, post.AuthorId, cancellationToken)) allowed.Add(post);
        }
        var items = await MapManyAsync(allowed, cancellationToken);
        return new(items, page.SafePageNumber, page.SafePageSize, total);
    }

    public async Task<PostResponse> GetAsync(Guid userId, Guid postId, CancellationToken cancellationToken)
    {
        var post = await db.Posts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == postId && !x.IsDeleted && !x.IsHidden, cancellationToken) ?? throw new KeyNotFoundException("Post was not found.");
        if (!await CanViewAsync(userId, post, cancellationToken)) throw new KeyNotFoundException("Post was not found.");
        return (await MapManyAsync([post], cancellationToken)).Single();
    }

    public async Task<PagedResult<CommentResponse>> ListCommentsAsync(Guid userId, Guid postId, PagedRequest page, CancellationToken cancellationToken)
    {
        var post = await db.Posts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == postId && !x.IsDeleted && !x.IsHidden, cancellationToken)
            ?? throw new KeyNotFoundException("Post was not found.");
        if (!await CanViewAsync(userId, post, cancellationToken)) throw new KeyNotFoundException("Post was not found.");
        var query = db.Comments.AsNoTracking().Where(x => x.PostId == postId && !x.IsDeleted && !x.IsHidden);
        var total = await query.LongCountAsync(cancellationToken);
        var comments = await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Skip(page.Skip).Take(page.SafePageSize).ToListAsync(cancellationToken);
        var authorIds = comments.Select(x => x.AuthorId).Distinct().ToArray();
        var authors = await (from user in db.Users.AsNoTracking()
            join profile in db.Profiles.AsNoTracking() on user.Id equals profile.Id
            where authorIds.Contains(user.Id)
            select new { user.Id, Name = user.FirstName + " " + user.LastName, profile.ProfileImageUrl })
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var items = comments.Select(comment =>
        {
            var author = authors.GetValueOrDefault(comment.AuthorId);
            return new CommentResponse(comment.Id, comment.PostId, comment.AuthorId, comment.Content, comment.CreatedAtUtc,
                author?.Name ?? "Community member", author?.ProfileImageUrl);
        }).ToList();
        return new(items, page.SafePageNumber, page.SafePageSize, total);
    }
    public async Task<PostResponse> UpdateAsync(Guid userId, Guid postId, UpdatePostRequest request, CancellationToken cancellationToken)
    {
        var post = await db.Posts.SingleOrDefaultAsync(x => x.Id == postId && !x.IsDeleted, cancellationToken) ?? throw new KeyNotFoundException("Post was not found.");
        if (post.AuthorId != userId) throw new AccessDeniedException("Only the post author can edit it.");
        post.Update(request.Content, request.AllowComments, clock.UtcNow); await db.SaveChangesAsync(cancellationToken); return await GetAsync(userId, postId, cancellationToken);
    }

    public async Task DeleteAsync(Guid userId, Guid postId, CancellationToken cancellationToken)
    {
        var post = await db.Posts.SingleOrDefaultAsync(x => x.Id == postId && !x.IsDeleted, cancellationToken) ?? throw new KeyNotFoundException("Post was not found.");
        if (post.AuthorId != userId) throw new AccessDeniedException("Only the post author can delete it.");
        post.SoftDelete(clock.UtcNow); await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CommentResponse> AddCommentAsync(Guid userId, Guid postId, CreateCommentRequest request, CancellationToken cancellationToken)
    {
        var post = await db.Posts.SingleOrDefaultAsync(x => x.Id == postId && !x.IsDeleted, cancellationToken) ?? throw new KeyNotFoundException("Post was not found.");
        if (!await CanViewAsync(userId, post, cancellationToken)) throw new KeyNotFoundException("Post was not found.");
        if (!post.AllowComments) throw new InvalidOperationException("Comments are disabled for this post.");
        var comment = new Comment(postId, userId, request.Content, clock.UtcNow); await db.Comments.AddAsync(comment, cancellationToken); await db.SaveChangesAsync(cancellationToken);
        return (await MapCommentsAsync([comment], cancellationToken)).Single();
    }

    public async Task DeleteCommentAsync(Guid userId, Guid commentId, CancellationToken cancellationToken)
    {
        var comment = await db.Comments.SingleOrDefaultAsync(x => x.Id == commentId && !x.IsDeleted, cancellationToken) ?? throw new KeyNotFoundException("Comment was not found.");
        if (comment.AuthorId != userId) throw new AccessDeniedException("Only the comment author can remove it.");
        comment.SoftDelete(clock.UtcNow); await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CommentResponse> UpdateCommentAsync(Guid userId, Guid commentId, CreateCommentRequest request, CancellationToken cancellationToken)
    {
        var comment = await db.Comments.SingleOrDefaultAsync(x => x.Id == commentId && !x.IsDeleted && !x.IsHidden, cancellationToken) ?? throw new KeyNotFoundException("Comment was not found.");
        if (comment.AuthorId != userId) throw new AccessDeniedException("Only the comment author can edit it.");
        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > 4000) throw new ArgumentException("Comment content must be between 1 and 4000 characters.");
        var post = await db.Posts.SingleOrDefaultAsync(x => x.Id == comment.PostId && !x.IsDeleted && !x.IsHidden, cancellationToken) ?? throw new KeyNotFoundException("Post was not found.");
        if (!await CanViewAsync(userId, post, cancellationToken)) throw new KeyNotFoundException("Comment was not found.");
        comment.Update(request.Content, clock.UtcNow); await db.SaveChangesAsync(cancellationToken);
        return (await MapCommentsAsync([comment], cancellationToken)).Single();
    }

    public async Task SetReactionAsync(Guid userId, Guid postId, ReactionType type, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentException("Reaction type is invalid.");
        _ = await GetAsync(userId, postId, cancellationToken);
        var reaction = await db.PostReactions.SingleOrDefaultAsync(x => x.PostId == postId && x.UserId == userId, cancellationToken);
        if (reaction is not null) db.PostReactions.Remove(reaction);
        await db.PostReactions.AddAsync(new PostReaction(postId, userId, type, clock.UtcNow), cancellationToken); await db.SaveChangesAsync(cancellationToken);
    }
    public async Task RemoveReactionAsync(Guid userId, Guid postId, CancellationToken cancellationToken)
    {
        var reaction = await db.PostReactions.SingleOrDefaultAsync(x => x.PostId == postId && x.UserId == userId, cancellationToken);
        if (reaction is not null) { db.PostReactions.Remove(reaction); await db.SaveChangesAsync(cancellationToken); }
    }
    public async Task SetBookmarkAsync(Guid userId, Guid postId, CancellationToken cancellationToken)
    {
        _ = await GetAsync(userId, postId, cancellationToken);
        if (!await db.PostBookmarks.AnyAsync(x => x.PostId == postId && x.UserId == userId, cancellationToken))
        { await db.PostBookmarks.AddAsync(new PostBookmark(postId, userId, clock.UtcNow), cancellationToken); await db.SaveChangesAsync(cancellationToken); }
    }
    public async Task RemoveBookmarkAsync(Guid userId, Guid postId, CancellationToken cancellationToken)
    {
        var bookmark = await db.PostBookmarks.SingleOrDefaultAsync(x => x.PostId == postId && x.UserId == userId, cancellationToken);
        if (bookmark is not null) { db.PostBookmarks.Remove(bookmark); await db.SaveChangesAsync(cancellationToken); }
    }
    public async Task<PagedResult<PostResponse>> BookmarksAsync(Guid userId, PagedRequest page, CancellationToken cancellationToken)
    {
        var query = db.PostBookmarks.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAtUtc);
        var ids = await query.Select(x => x.PostId).ToListAsync(cancellationToken);
        var eligible = new List<Post>();
        foreach (var id in ids) { var p = await db.Posts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken); if (p is not null && await CanViewAsync(userId, p, cancellationToken)) eligible.Add(p); }
        var count = eligible.Count; var selected = eligible.OrderByDescending(x => x.CreatedAtUtc).Skip(page.Skip).Take(page.SafePageSize).ToList();
        return new(await MapManyAsync(selected, cancellationToken), page.SafePageNumber, page.SafePageSize, count);
    }

    private async Task<bool> CanViewAsync(Guid userId, Post post, CancellationToken ct)
    {
        if (post.AuthorId == userId) return true;
        if (post.IsHidden || await db.BlockedUsers.AnyAsync(x => (x.BlockerId == userId && x.BlockedId == post.AuthorId) || (x.BlockerId == post.AuthorId && x.BlockedId == userId), ct)) return false;
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, ct);
        if (user is null) return false;
        return post.Audience switch
        {
            PostAudience.UniversityCommunity => post.UniversityId == user.UniversityId && await IsProfileVisibleAsync(userId, post.AuthorId, ct),
            PostAudience.StudentsOnly => post.UniversityId == user.UniversityId && (user.JourneyStage is JourneyStage.NewStudent or JourneyStage.CurrentStudent) && await IsProfileVisibleAsync(userId, post.AuthorId, ct),
            PostAudience.GraduatesAndAlumni => post.UniversityId == user.UniversityId && (user.JourneyStage is JourneyStage.Graduate or JourneyStage.Alumni) && await IsProfileVisibleAsync(userId, post.AuthorId, ct),
            PostAudience.ConnectionsOnly => await AreConnected(userId, post.AuthorId, ct),
            PostAudience.CommunityMembers => post.CommunityId is { } communityId && await db.CommunityMembers.AnyAsync(x => x.CommunityId == communityId && x.UserId == userId, ct) && await IsProfileVisibleAsync(userId, post.AuthorId, ct),
            PostAudience.SelectedUsers => await db.PostAudienceUsers.AnyAsync(x => x.PostId == post.Id && x.UserId == userId, ct) && await IsProfileVisibleAsync(userId, post.AuthorId, ct),
            _ => false
        };
    }
    private async Task<bool> IsProfileVisibleAsync(Guid viewerId, Guid authorId, CancellationToken ct)
    {
        var profile = await db.Profiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == authorId, ct);
        if (profile is null || profile.Visibility == ProfileVisibility.Private) return false;
        if (profile.Visibility == ProfileVisibility.ConnectionsOnly) return await AreConnected(viewerId, authorId, ct);
        var viewer = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == viewerId && x.IsActive, ct);
        var author = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == authorId && x.IsActive, ct);
        return viewer is not null && author is not null && viewer.UniversityId == author.UniversityId;
    }
    private Task<bool> AreConnected(Guid a, Guid b, CancellationToken ct) => db.Connections.AnyAsync(x => (x.UserId == a && x.ConnectedUserId == b) || (x.UserId == b && x.ConnectedUserId == a), ct);

    private async Task<IReadOnlyList<CommentResponse>> MapCommentsAsync(IReadOnlyList<Comment> comments, CancellationToken ct)
    {
        if (comments.Count == 0) return [];
        var authorIds = comments.Select(x => x.AuthorId).Distinct().ToArray();
        var authors = await (from user in db.Users.AsNoTracking()
            join profile in db.Profiles.AsNoTracking() on user.Id equals profile.Id
            where authorIds.Contains(user.Id)
            select new { user.Id, Name = user.FirstName + " " + user.LastName, profile.ProfileImageUrl })
            .ToDictionaryAsync(x => x.Id, ct);
        return comments.Select(comment =>
        {
            var author = authors.GetValueOrDefault(comment.AuthorId);
            return new CommentResponse(comment.Id, comment.PostId, comment.AuthorId, comment.Content, comment.CreatedAtUtc,
                author?.Name ?? "Community member", author?.ProfileImageUrl);
        }).ToList();
    }
    private async Task<IReadOnlyList<PostResponse>> MapManyAsync(IReadOnlyList<Post> posts, CancellationToken ct)
    {
        if (posts.Count == 0) return [];
        var ids = posts.Select(x => x.Id).ToArray(); var userIds = posts.Select(x => x.AuthorId).Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FirstName + " " + x.LastName, ct);
        var comments = await db.Comments.AsNoTracking().Where(x => ids.Contains(x.PostId) && !x.IsDeleted && !x.IsHidden).GroupBy(x => x.PostId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        var reactions = await db.PostReactions.AsNoTracking().Where(x => ids.Contains(x.PostId)).GroupBy(x => x.PostId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        return posts.Select(x => new PostResponse(x.Id, x.AuthorId, names.GetValueOrDefault(x.AuthorId, "Member"), x.Content, x.Audience, x.CommunityId, x.AllowComments, x.CreatedAtUtc, comments.GetValueOrDefault(x.Id), reactions.GetValueOrDefault(x.Id))).ToList();
    }
}
