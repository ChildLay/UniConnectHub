using UniConnect.Domain.Common;
using UniConnect.Domain.Universities;

namespace UniConnect.Domain.Mentoring;

public enum MentorshipStatus { Pending, Accepted, Declined, Completed, Cancelled }

public sealed class MentorProfile : Entity<Guid>, IAuditableEntity
{
    private MentorProfile() : base(Guid.Empty) { }
    public MentorProfile(Guid userId, string bio, int? capacity, DateTime nowUtc) : base(userId)
    {
        if (capacity is <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        Bio = University.Required(bio, nameof(bio), 3000); Capacity = capacity; IsAvailable = true; CreatedAtUtc = UpdatedAtUtc = nowUtc;
    }
    public string Bio { get; private set; } = string.Empty;
    public int? Capacity { get; private set; }
    public bool IsAvailable { get; private set; }
    public int ActiveMenteeCount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public void SetAvailability(bool available, DateTime nowUtc) { IsAvailable = available; UpdatedAtUtc = nowUtc; }
    public void SetActiveMenteeCount(int count, DateTime nowUtc) { ActiveMenteeCount = count; UpdatedAtUtc = nowUtc; }
    public void Update(string bio, int? capacity, bool available, int activeMenteeCount, DateTime nowUtc)
    {
        if (capacity is <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (capacity is { } limit && limit < activeMenteeCount)
            throw new InvalidOperationException("Capacity cannot be lower than the current number of active mentees.");
        Bio = University.Required(bio, nameof(bio), 3000);
        Capacity = capacity;
        IsAvailable = available;
        ActiveMenteeCount = activeMenteeCount;
        UpdatedAtUtc = nowUtc;
    }
}

public sealed class MentorshipRequest : Entity<Guid>, IAuditableEntity
{
    private MentorshipRequest() : base(Guid.Empty) { }
    public MentorshipRequest(Guid mentorId, Guid menteeId, string message, DateTime nowUtc) : base(Guid.NewGuid())
    {
        if (mentorId == menteeId) throw new InvalidOperationException("A user cannot mentor themselves.");
        MentorId = mentorId; MenteeId = menteeId; Message = University.Required(message, nameof(message), 2000); Status = MentorshipStatus.Pending; CreatedAtUtc = UpdatedAtUtc = nowUtc;
    }
    public Guid MentorId { get; private set; }
    public Guid MenteeId { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public MentorshipStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public void Transition(MentorshipStatus status, DateTime nowUtc)
    {
        var valid = Status == MentorshipStatus.Pending && status is MentorshipStatus.Accepted or MentorshipStatus.Declined or MentorshipStatus.Cancelled
            || Status == MentorshipStatus.Accepted && status is MentorshipStatus.Completed or MentorshipStatus.Cancelled;
        if (!valid) throw new InvalidOperationException($"Cannot transition mentorship from {Status} to {status}.");
        Status = status; UpdatedAtUtc = nowUtc;
    }
}
