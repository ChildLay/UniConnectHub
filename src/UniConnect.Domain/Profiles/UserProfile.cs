using UniConnect.Domain.Common;
using UniConnect.Domain.Universities;

namespace UniConnect.Domain.Profiles;

public enum ProfileVisibility { UniversityCommunity, ConnectionsOnly, Private }

public sealed class UserProfile : Entity<Guid>, IAuditableEntity
{
    private UserProfile() : base(Guid.Empty) { }
    public UserProfile(Guid userId, DateTime nowUtc) : base(userId) => CreatedAtUtc = UpdatedAtUtc = nowUtc;

    public string? Headline { get; private set; }
    public string? Bio { get; private set; }
    public string? Programme { get; private set; }
    public string? StudyArea { get; private set; }
    public int? GraduationYear { get; private set; }
    public string? IndustryExperience { get; private set; }
    public string? CareerGoals { get; private set; }
    public string? VolunteeringInterests { get; private set; }
    public string? Hobbies { get; private set; }
    public string? Languages { get; private set; }
    public string? ProfileImageUrl { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public ProfileVisibility Visibility { get; private set; } = ProfileVisibility.UniversityCommunity;
    public bool AllowConnectionRequests { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(string? headline, string? bio, string? programme, string? studyArea, int? graduationYear,
        string? industryExperience, string? careerGoals, string? volunteeringInterests, string? hobbies,
        string? languages, string? profileImageUrl, ProfileVisibility visibility, bool allowConnectionRequests, DateTime nowUtc)
    {
        Headline = Trim(headline, 160); Bio = Trim(bio, 3000); Programme = Trim(programme, 200); StudyArea = Trim(studyArea, 200);
        if (graduationYear is < 1900 or > 2200) throw new ArgumentOutOfRangeException(nameof(graduationYear));
        GraduationYear = graduationYear; IndustryExperience = Trim(industryExperience, 1000); CareerGoals = Trim(careerGoals, 1000);
        VolunteeringInterests = Trim(volunteeringInterests, 1000); Hobbies = Trim(hobbies, 1000); Languages = Trim(languages, 500);
        ProfileImageUrl = Trim(profileImageUrl, 1000); Visibility = visibility; AllowConnectionRequests = allowConnectionRequests; UpdatedAtUtc = nowUtc;
    }

    public void SetProfileImageUrl(string? imageUrl, DateTime nowUtc) { ProfileImageUrl = Trim(imageUrl, 1000); UpdatedAtUtc = nowUtc; }
    public void SetCoverImageUrl(string? imageUrl, DateTime nowUtc) { CoverImageUrl = Trim(imageUrl, 1000); UpdatedAtUtc = nowUtc; }

    private static string? Trim(string? value, int max) => University.Optional(value, max);
}
