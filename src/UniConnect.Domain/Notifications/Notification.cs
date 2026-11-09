using UniConnect.Domain.Common;
using UniConnect.Domain.Universities;

namespace UniConnect.Domain.Notifications;

public sealed class Notification : Entity<Guid>, IAuditableEntity
{
    private Notification() : base(Guid.Empty) { }
    public Notification(Guid userId, string type, string title, string message, string? resourceType, Guid? resourceId, DateTime createdAtUtc) : base(Guid.NewGuid())
    {
        UserId = userId; Type = University.Required(type, nameof(type), 80); Title = University.Required(title, nameof(title), 160);
        Message = University.Required(message, nameof(message), 1000); ResourceType = University.Optional(resourceType, 80);
        ResourceId = resourceId; CreatedAtUtc = UpdatedAtUtc = createdAtUtc;
    }
    public Guid UserId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string? ResourceType { get; private set; }
    public Guid? ResourceId { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public bool IsHidden { get; private set; }
    public void Hide(DateTime nowUtc) { IsHidden = true; UpdatedAtUtc = nowUtc; }
    public void MarkRead(DateTime nowUtc) { if (!IsRead) { IsRead = true; ReadAtUtc = nowUtc; UpdatedAtUtc = nowUtc; } }
}
