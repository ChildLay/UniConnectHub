using UniConnect.Domain.Connections;
using UniConnect.Domain.Events;
using UniConnect.Domain.Mentoring;
using UniConnect.Domain.Universities;
using UniConnect.Domain.Users;

namespace UniConnect.Domain.Tests;

public sealed class DomainInvariantTests
{
    [Fact]
    public void User_cannot_connect_to_self()
    {
        var userId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => new ConnectionRequest(userId, userId, DateTime.UtcNow));
    }

    [Fact]
    public void User_cannot_mentor_self()
    {
        var userId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => new MentorshipRequest(userId, userId, "Please mentor me", DateTime.UtcNow));
    }

    [Fact]
    public void Registration_requires_active_university()
    {
        var university = new University("Example University", null, "NZ", null, null, DateTime.UtcNow);
        university.SetActive(false, DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => new User("a@example.com", "hash", "A", "B", JourneyStage.NewStudent, university, DateTime.UtcNow));
    }

    [Fact]
    public void Event_requires_end_after_start()
    {
        var start = DateTime.UtcNow;
        Assert.Throws<ArgumentException>(() => new Event("Event", null, Guid.NewGuid(), Guid.NewGuid(), start, start, null, EventType.Physical, start));
    }

    [Fact]
    public void Mentorship_transitions_follow_state_rules()
    {
        var now = DateTime.UtcNow;
        var request = new MentorshipRequest(Guid.NewGuid(), Guid.NewGuid(), "Would you mentor me?", now);
        request.Transition(MentorshipStatus.Accepted, now);
        request.Transition(MentorshipStatus.Completed, now);
        Assert.Throws<InvalidOperationException>(() => request.Transition(MentorshipStatus.Accepted, now));
    }
}
