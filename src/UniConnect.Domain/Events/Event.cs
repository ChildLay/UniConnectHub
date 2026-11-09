using UniConnect.Domain.Common;
using UniConnect.Domain.Universities;

namespace UniConnect.Domain.Events;

public enum EventType { Physical, Online, Hybrid }
public enum EventStatus { Draft, Published, Cancelled, Completed }
public enum EventRegistrationStatus { Registered, Waitlisted, Cancelled }

public sealed class Event : Entity<Guid>, IAuditableEntity
{
    private Event() : base(Guid.Empty) { }
    public Event(string title, string? description, Guid hostId, Guid universityId, DateTime startUtc, DateTime endUtc, int? capacity, EventType type, DateTime nowUtc, Guid? communityId = null) : base(Guid.NewGuid())
    {
        if (endUtc <= startUtc) throw new ArgumentException("Event end must be after its start.");
        if (capacity is <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        Title = University.Required(title, nameof(title), 200); Description = University.Optional(description, 5000);
        HostId = hostId; UniversityId = universityId; CommunityId = communityId; StartUtc = startUtc; EndUtc = endUtc; Capacity = capacity; Type = type;
        Status = EventStatus.Draft; CreatedAtUtc = UpdatedAtUtc = nowUtc;
    }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid HostId { get; private set; }
    public Guid UniversityId { get; private set; }
    public Guid? CommunityId { get; private set; }
    public DateTime StartUtc { get; private set; }
    public DateTime EndUtc { get; private set; }
    public int? Capacity { get; private set; }
    public EventType Type { get; private set; }
    public EventStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public void Publish(DateTime nowUtc) { if (Status != EventStatus.Draft) throw new InvalidOperationException("Only draft events can be published."); Status = EventStatus.Published; UpdatedAtUtc = nowUtc; }
    public void Cancel(DateTime nowUtc) { if (Status is EventStatus.Cancelled or EventStatus.Completed) throw new InvalidOperationException("This event cannot be cancelled."); Status = EventStatus.Cancelled; UpdatedAtUtc = nowUtc; }
    public void Update(string title, string? description, DateTime startUtc, DateTime endUtc, int? capacity, EventType type, DateTime nowUtc)
    {
        if (Status is EventStatus.Cancelled or EventStatus.Completed) throw new InvalidOperationException("This event cannot be changed.");
        if (endUtc <= startUtc) throw new ArgumentException("Event end must be after its start.");
        if (capacity is <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        Title = University.Required(title, nameof(title), 200); Description = University.Optional(description, 5000);
        StartUtc = startUtc; EndUtc = endUtc; Capacity = capacity; Type = type; UpdatedAtUtc = nowUtc;
    }
}

public sealed class EventRegistration
{
    private EventRegistration() { }
    public EventRegistration(Guid eventId, Guid userId, EventRegistrationStatus status, DateTime createdAtUtc) { EventId = eventId; UserId = userId; Status = status; CreatedAtUtc = createdAtUtc; }
    public Guid EventId { get; private set; }
    public Guid UserId { get; private set; }
    public EventRegistrationStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
