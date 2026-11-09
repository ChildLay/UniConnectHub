using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Common;
using UniConnect.Domain.Connections;
using UniConnect.Domain.Profiles;
using UniConnect.Domain.Users;

namespace UniConnect.Application.Profiles;

public sealed record ProfileResponse(Guid UserId, string FirstName, string LastName, Guid UniversityId, JourneyStage JourneyStage,
    string? Headline, string? Bio, string? Programme, string? StudyArea, int? GraduationYear,
    string? IndustryExperience, string? CareerGoals, string? VolunteeringInterests, string? Hobbies,
    string? Languages, string? ProfileImageUrl, string? CoverImageUrl, ProfileVisibility Visibility, bool AllowConnectionRequests,
    IReadOnlyList<string> Skills, IReadOnlyList<string> Interests);

public sealed record UpdateProfileRequest(string? Headline, string? Bio, string? Programme, string? StudyArea,
    int? GraduationYear, string? IndustryExperience, string? CareerGoals, string? VolunteeringInterests,
    string? Hobbies, string? Languages, string? ProfileImageUrl, ProfileVisibility Visibility, bool AllowConnectionRequests);

public interface IProfileService
{
    Task<ProfileResponse> GetAsync(Guid viewerId, Guid subjectId, CancellationToken cancellationToken);
    Task<ProfileResponse> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken);
    Task<ProfileResponse> SetProfileImageAsync(Guid userId, string? imageUrl, CancellationToken cancellationToken);
    Task<ProfileResponse> SetCoverImageAsync(Guid userId, string? imageUrl, CancellationToken cancellationToken);
    Task<ProfileResponse> SetSkillsAsync(Guid userId, IReadOnlyCollection<string> names, CancellationToken cancellationToken);
    Task<ProfileResponse> SetInterestsAsync(Guid userId, IReadOnlyCollection<string> names, CancellationToken cancellationToken);
}

public sealed class ProfileService(IApplicationDbContext db, IClock clock) : IProfileService
{
    public async Task<ProfileResponse> GetAsync(Guid viewerId, Guid subjectId, CancellationToken cancellationToken)
    {
        var subject = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == subjectId && x.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("Profile was not found.");
        var profile = await db.Profiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == subjectId, cancellationToken)
            ?? throw new KeyNotFoundException("Profile was not found.");
        var blocked = await db.BlockedUsers.AnyAsync(x => (x.BlockerId == viewerId && x.BlockedId == subjectId) || (x.BlockerId == subjectId && x.BlockedId == viewerId), cancellationToken);
        var connected = await db.Connections.AnyAsync(x => (x.UserId == viewerId && x.ConnectedUserId == subjectId) || (x.UserId == subjectId && x.ConnectedUserId == viewerId), cancellationToken);
        var viewer = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == viewerId && x.IsActive, cancellationToken);
        var allowed = viewerId == subjectId || (!blocked && profile.Visibility switch
        {
            ProfileVisibility.Private => false,
            ProfileVisibility.ConnectionsOnly => connected,
            ProfileVisibility.UniversityCommunity => viewer?.UniversityId == subject.UniversityId,
            _ => true
        });
        if (!allowed) throw new AccessDeniedException("You are not allowed to view this profile.");
        return await MapAsync(subject, profile, cancellationToken);
    }

    public async Task<ProfileResponse> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var profile = await db.Profiles.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Profile was not found.");
        if (!Enum.IsDefined(request.Visibility)) throw new ArgumentException("Profile visibility is invalid.");
        profile.Update(request.Headline, request.Bio, request.Programme, request.StudyArea, request.GraduationYear,
            request.IndustryExperience, request.CareerGoals, request.VolunteeringInterests, request.Hobbies,
            request.Languages, request.ProfileImageUrl, request.Visibility, request.AllowConnectionRequests, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, userId, cancellationToken);
    }

    public async Task<ProfileResponse> SetProfileImageAsync(Guid userId, string? imageUrl, CancellationToken cancellationToken) => await SetImageAsync(userId, imageUrl, false, cancellationToken);
    public async Task<ProfileResponse> SetCoverImageAsync(Guid userId, string? imageUrl, CancellationToken cancellationToken) => await SetImageAsync(userId, imageUrl, true, cancellationToken);

    private async Task<ProfileResponse> SetImageAsync(Guid userId, string? imageUrl, bool cover, CancellationToken cancellationToken)
    {
        var profile = await db.Profiles.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new KeyNotFoundException("Profile was not found.");
        if (cover) profile.SetCoverImageUrl(imageUrl, clock.UtcNow); else profile.SetProfileImageUrl(imageUrl, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, userId, cancellationToken);
    }

    public Task<ProfileResponse> SetSkillsAsync(Guid userId, IReadOnlyCollection<string> names, CancellationToken cancellationToken) => SetTaxonomyAsync(userId, names, true, cancellationToken);
    public Task<ProfileResponse> SetInterestsAsync(Guid userId, IReadOnlyCollection<string> names, CancellationToken cancellationToken) => SetTaxonomyAsync(userId, names, false, cancellationToken);

    private async Task<ProfileResponse> SetTaxonomyAsync(Guid userId, IReadOnlyCollection<string> names, bool skills, CancellationToken cancellationToken)
    {
        var normalized = names.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (normalized.Length > 50) throw new ArgumentException("At most 50 values may be supplied.");
        if (skills)
        {
            var old = await db.UserSkills.Where(x => x.UserId == userId).ToListAsync(cancellationToken); db.UserSkills.RemoveRange(old);
            foreach (var name in normalized)
            {
                var key = name.ToUpperInvariant(); var item = await db.Skills.SingleOrDefaultAsync(x => x.NormalizedName == key, cancellationToken);
                if (item is null) { item = new Skill(name); await db.Skills.AddAsync(item, cancellationToken); }
                await db.UserSkills.AddAsync(new UserSkill(userId, item.Id), cancellationToken);
            }
        }
        else
        {
            var old = await db.UserInterests.Where(x => x.UserId == userId).ToListAsync(cancellationToken); db.UserInterests.RemoveRange(old);
            foreach (var name in normalized)
            {
                var key = name.ToUpperInvariant(); var item = await db.Interests.SingleOrDefaultAsync(x => x.NormalizedName == key, cancellationToken);
                if (item is null) { item = new Interest(name); await db.Interests.AddAsync(item, cancellationToken); }
                await db.UserInterests.AddAsync(new UserInterest(userId, item.Id), cancellationToken);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, userId, cancellationToken);
    }

    private async Task<ProfileResponse> MapAsync(User user, UserProfile profile, CancellationToken cancellationToken)
    {
        var skills = await (from us in db.UserSkills.AsNoTracking() join s in db.Skills.AsNoTracking() on us.SkillId equals s.Id where us.UserId == user.Id select s.Name).ToListAsync(cancellationToken);
        var interests = await (from ui in db.UserInterests.AsNoTracking() join i in db.Interests.AsNoTracking() on ui.InterestId equals i.Id where ui.UserId == user.Id select i.Name).ToListAsync(cancellationToken);
        return new(user.Id, user.FirstName, user.LastName, user.UniversityId, user.JourneyStage, profile.Headline, profile.Bio,
            profile.Programme, profile.StudyArea, profile.GraduationYear, profile.IndustryExperience, profile.CareerGoals,
            profile.VolunteeringInterests, profile.Hobbies, profile.Languages, profile.ProfileImageUrl, profile.CoverImageUrl, profile.Visibility,
            profile.AllowConnectionRequests, skills, interests);
    }
}
