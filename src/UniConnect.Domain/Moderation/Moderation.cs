using UniConnect.Domain.Common;
using UniConnect.Domain.Universities;

namespace UniConnect.Domain.Moderation;

public enum ReportStatus { Open, UnderReview, Resolved, Dismissed }
public enum UserRole { User, Admin, Moderator }
public enum ReportedTargetType { User, Post, Comment, Community }

public sealed class Report : Entity<Guid>, IAuditableEntity
{
    private Report() : base(Guid.Empty) { }
    public Report(Guid reporterId, string targetType, Guid targetId, string reason, string? details, DateTime nowUtc) : base(Guid.NewGuid())
    {
        ReporterId = reporterId; TargetType = University.Required(targetType, nameof(targetType), 80); TargetId = targetId;
        Reason = University.Required(reason, nameof(reason), 300); Details = University.Optional(details, 3000); Status = ReportStatus.Open;
        CreatedAtUtc = UpdatedAtUtc = nowUtc;
    }
    public Guid ReporterId { get; private set; }
    public string TargetType { get; private set; } = string.Empty;
    public Guid TargetId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string? Details { get; private set; }
    public ReportStatus Status { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public string? Resolution { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public void Review(Guid reviewerId, ReportStatus status, string? resolution, DateTime nowUtc)
    {
        if (status is not (ReportStatus.UnderReview or ReportStatus.Resolved or ReportStatus.Dismissed)) throw new ArgumentOutOfRangeException(nameof(status));
        ReviewedByUserId = reviewerId; Status = status; Resolution = University.Optional(resolution, 2000); UpdatedAtUtc = nowUtc;
    }
}

public sealed class AuditLog : Entity<Guid>
{
    private AuditLog() : base(Guid.Empty) { }
    public AuditLog(Guid? userId, string action, string entityType, Guid? entityId, string? metadataJson, DateTime timestampUtc) : base(Guid.NewGuid())
    {
        UserId = userId; Action = University.Required(action, nameof(action), 100); EntityType = University.Required(entityType, nameof(entityType), 100);
        EntityId = entityId; MetadataJson = University.Optional(metadataJson, 4000); TimestampUtc = timestampUtc;
    }
    public Guid? UserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public Guid? EntityId { get; private set; }
    public string? MetadataJson { get; private set; }
    public DateTime TimestampUtc { get; private set; }
}

public sealed class ModerationAction : Entity<Guid>
{
    private ModerationAction() : base(Guid.Empty) { }
    public ModerationAction(Guid moderatorId, string action, string targetType, Guid targetId, string? details, DateTime nowUtc) : base(Guid.NewGuid())
    {
        ModeratorId = moderatorId; Action = University.Required(action, nameof(action), 80);
        TargetType = University.Required(targetType, nameof(targetType), 80); TargetId = targetId;
        Details = University.Optional(details, 2000); CreatedAtUtc = nowUtc;
    }
    public Guid ModeratorId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string TargetType { get; private set; } = string.Empty;
    public Guid TargetId { get; private set; }
    public string? Details { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
