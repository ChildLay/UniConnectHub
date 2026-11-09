using UniConnect.Application.Common;
using UniConnect.Application.Mentoring;
using UniConnect.Domain.Users;

namespace UniConnect.Application.Tests;

public sealed class MentorEligibilityTests
{
    [Theory]
    [InlineData(JourneyStage.NewStudent)]
    [InlineData(JourneyStage.CurrentStudent)]
    [InlineData(JourneyStage.Graduate)]
    public void Non_alumni_cannot_create_or_update_a_mentor_profile(JourneyStage stage)
    {
        var exception = Assert.Throws<AccessDeniedException>(() => MentorEligibility.EnsureAlumni(stage));
        Assert.Equal("Only alumni can create or update a mentor profile.", exception.Message);
    }

    [Fact]
    public void Alumni_can_create_or_update_a_mentor_profile()
    {
        MentorEligibility.EnsureAlumni(JourneyStage.Alumni);
    }
    [Fact]
    public void A_user_cannot_request_themselves_as_a_mentor()
    {
        var userId = Guid.NewGuid();
        var exception = Assert.Throws<ArgumentException>(() => MentorEligibility.EnsureNotSelf(userId, userId));
        Assert.Equal("A user cannot mentor themselves.", exception.Message);
    }

    [Fact]
    public void A_user_can_request_a_different_mentor()
    {
        MentorEligibility.EnsureNotSelf(Guid.NewGuid(), Guid.NewGuid());
    }
}