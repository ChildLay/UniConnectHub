using UniConnect.Domain.Common;
using UniConnect.Domain.Universities;

namespace UniConnect.Domain.Communities;

public enum CommunityVisibility { Public, UniversityOnly, Private }
public enum CommunityMemberRole { Member, Moderator, Admin }

public sealed class Community : Entity<Guid>, IAuditableEntity
{
    private Community() : base(Guid.Empty) { }
    public Community(string name, string? description, Guid universityId, Guid ownerId, CommunityVisibility visibility, DateTime nowUtc) : base(Guid.NewGuid())
    {
        Name = Required(name, nameof(name), 160); Description = Optional(description, 3000); UniversityId = universityId; OwnerId = ownerId;
        Visibility = visibility; IsActive = true; CreatedAtUtc = UpdatedAtUtc = nowUtc;
    }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid UniversityId { get; private set; }
    public Guid OwnerId { get; private set; }
    public CommunityVisibility Visibility { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsHidden { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public void Update(string name, string? description, CommunityVisibility visibility, DateTime nowUtc)
    {
        if (!IsActive) throw new InvalidOperationException("Inactive communities cannot be edited.");
        Name = Required(name, nameof(name), 160); Description = Optional(description, 3000); Visibility = visibility; UpdatedAtUtc = nowUtc;
    }
    public void Deactivate(DateTime nowUtc) { IsActive = false; UpdatedAtUtc = nowUtc; }
    public void Hide(DateTime nowUtc) { IsHidden = true; UpdatedAtUtc = nowUtc; }
    private static string Required(string value, string field, int max) => University.Required(value, field, max);
    private static string? Optional(string? value, int max) => University.Optional(value, max);
}

public sealed class CommunityMember
{
    private CommunityMember() { }
    public CommunityMember(Guid communityId, Guid userId, CommunityMemberRole role, DateTime joinedAtUtc) { CommunityId = communityId; UserId = userId; Role = role; JoinedAtUtc = joinedAtUtc; }
    public Guid CommunityId { get; private set; }
    public Guid UserId { get; private set; }
    public CommunityMemberRole Role { get; private set; }
    public DateTime JoinedAtUtc { get; private set; }
}
