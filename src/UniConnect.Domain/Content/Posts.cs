using UniConnect.Domain.Common;
using UniConnect.Domain.Communities;
using UniConnect.Domain.Profiles;
using UniConnect.Domain.Universities;

namespace UniConnect.Domain.Content;

public enum PostAudience { UniversityCommunity, StudentsOnly, GraduatesAndAlumni, ConnectionsOnly, CommunityMembers, SelectedUsers }
public enum ReactionType { Like, Celebrate, Support, Insightful }

public sealed class Post : Entity<Guid>, IAuditableEntity
{
    private Post() : base(Guid.Empty) { }
    public Post(Guid authorId, Guid universityId, string content, PostAudience audience, DateTime nowUtc, Guid? communityId = null, bool allowComments = true) : base(Guid.NewGuid())
    {
        AuthorId = authorId; UniversityId = universityId; Content = University.Required(content, nameof(content), 10000);
        Audience = audience; CommunityId = communityId; AllowComments = allowComments; CreatedAtUtc = UpdatedAtUtc = nowUtc;
        if (audience == PostAudience.CommunityMembers && communityId is null) throw new ArgumentException("Community posts require a community.", nameof(communityId));
    }
    public Guid AuthorId { get; private set; }
    public Guid UniversityId { get; private set; }
    public Guid? CommunityId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public PostAudience Audience { get; private set; }
    public bool AllowComments { get; private set; }
    public bool IsDeleted { get; private set; }
    public bool IsHidden { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public void Edit(string content, DateTime nowUtc) { if (IsDeleted) throw new InvalidOperationException("Deleted posts cannot be edited."); Content = University.Required(content, nameof(content), 10000); UpdatedAtUtc = nowUtc; }
    public void SoftDelete(DateTime nowUtc) { IsDeleted = true; UpdatedAtUtc = nowUtc; }
    public void Hide(DateTime nowUtc) { IsHidden = true; UpdatedAtUtc = nowUtc; }
    public void Update(string content, bool allowComments, DateTime nowUtc) { if (IsDeleted) throw new InvalidOperationException("Deleted posts cannot be edited."); Content = University.Required(content, nameof(content), 10000); AllowComments = allowComments; UpdatedAtUtc = nowUtc; }
}

public sealed class Comment : Entity<Guid>, IAuditableEntity
{
    private Comment() : base(Guid.Empty) { }
    public Comment(Guid postId, Guid authorId, string content, DateTime nowUtc) : base(Guid.NewGuid()) { PostId = postId; AuthorId = authorId; Content = University.Required(content, nameof(content), 4000); CreatedAtUtc = UpdatedAtUtc = nowUtc; }
    public Guid PostId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public bool IsDeleted { get; private set; }
    public bool IsHidden { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public void SoftDelete(DateTime nowUtc) { IsDeleted = true; UpdatedAtUtc = nowUtc; }
    public void Hide(DateTime nowUtc) { IsHidden = true; UpdatedAtUtc = nowUtc; }
    public void Update(string content, DateTime nowUtc) { if (IsDeleted) throw new InvalidOperationException("Deleted comments cannot be edited."); Content = University.Required(content, nameof(content), 4000); UpdatedAtUtc = nowUtc; }
}

public sealed class PostReaction
{
    private PostReaction() { }
    public PostReaction(Guid postId, Guid userId, ReactionType type, DateTime createdAtUtc) { PostId = postId; UserId = userId; Type = type; CreatedAtUtc = createdAtUtc; }
    public Guid PostId { get; private set; }
    public Guid UserId { get; private set; }
    public ReactionType Type { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}

public sealed class PostBookmark
{
    private PostBookmark() { }
    public PostBookmark(Guid postId, Guid userId, DateTime createdAtUtc) { PostId = postId; UserId = userId; CreatedAtUtc = createdAtUtc; }
    public Guid PostId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}

public sealed class PostAudienceUser
{
    private PostAudienceUser() { }
    public PostAudienceUser(Guid postId, Guid userId) { PostId = postId; UserId = userId; }
    public Guid PostId { get; private set; }
    public Guid UserId { get; private set; }
}
