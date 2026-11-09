using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Common;
using UniConnect.Domain.Connections;
using UniConnect.Domain.Profiles;
using UniConnect.Domain.Users;

namespace UniConnect.Application.Discovery;

public sealed record DiscoveryFilter(Guid? UniversityId, string? Programme, string? StudyArea, string? Skill,
    string? Interest, string? Industry, string? CareerGoal, JourneyStage? JourneyStage, int PageNumber = 1, int PageSize = 20);
public sealed record DiscoveredPerson(Guid UserId, string FirstName, string LastName, Guid UniversityId, JourneyStage JourneyStage,
    string? Headline, string? Programme, string? StudyArea, string? IndustryExperience, string? CareerGoals, string? ProfileImageUrl,
    IReadOnlyList<string> Skills, IReadOnlyList<string> Interests, int MatchScore);

public interface IDiscoveryService { Task<PagedResult<DiscoveredPerson>> SearchAsync(Guid viewerId, DiscoveryFilter filter, CancellationToken cancellationToken); }

public sealed class DiscoveryService(IApplicationDbContext db) : IDiscoveryService
{
    public async Task<PagedResult<DiscoveredPerson>> SearchAsync(Guid viewerId, DiscoveryFilter f, CancellationToken ct)
    {
        var viewer = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == viewerId && x.IsActive, ct)
            ?? throw new UnauthorizedAccessException("An active user is required.");
        var query = from user in db.Users.AsNoTracking()
                    join profile in db.Profiles.AsNoTracking() on user.Id equals profile.Id
                    where user.IsActive && user.Id != viewerId && profile.Visibility != ProfileVisibility.Private
                    select new { User = user, Profile = profile };
        if (f.UniversityId is { } universityId) query = query.Where(x => x.User.UniversityId == universityId);
        if (!string.IsNullOrWhiteSpace(f.Programme)) query = query.Where(x => x.Profile.Programme != null && x.Profile.Programme.Contains(f.Programme));
        if (!string.IsNullOrWhiteSpace(f.StudyArea)) query = query.Where(x => x.Profile.StudyArea != null && x.Profile.StudyArea.Contains(f.StudyArea));
        if (!string.IsNullOrWhiteSpace(f.Industry)) query = query.Where(x => x.Profile.IndustryExperience != null && x.Profile.IndustryExperience.Contains(f.Industry));
        if (!string.IsNullOrWhiteSpace(f.CareerGoal)) query = query.Where(x => x.Profile.CareerGoals != null && x.Profile.CareerGoals.Contains(f.CareerGoal));
        if (f.JourneyStage is { } stage) query = query.Where(x => x.User.JourneyStage == stage);
        var candidates = await query.ToListAsync(ct);
        var blocked = await db.BlockedUsers.AsNoTracking().Where(x => x.BlockerId == viewerId || x.BlockedId == viewerId)
            .Select(x => x.BlockerId == viewerId ? x.BlockedId : x.BlockerId).ToListAsync(ct);
        var connected = await db.Connections.AsNoTracking().Where(x => x.UserId == viewerId || x.ConnectedUserId == viewerId)
            .Select(x => x.UserId == viewerId ? x.ConnectedUserId : x.UserId).ToListAsync(ct);
        var visible = candidates.Where(x => !blocked.Contains(x.User.Id) &&
            (x.Profile.Visibility != ProfileVisibility.ConnectionsOnly || connected.Contains(x.User.Id)) &&
            (x.Profile.Visibility != ProfileVisibility.UniversityCommunity || viewer.UniversityId == x.User.UniversityId)).ToList();
        var userIds = visible.Select(x => x.User.Id).ToArray();
        var skillRows = await (from link in db.UserSkills.AsNoTracking() join skill in db.Skills.AsNoTracking() on link.SkillId equals skill.Id where userIds.Contains(link.UserId) select new { link.UserId, skill.Name }).ToListAsync(ct);
        var interestRows = await (from link in db.UserInterests.AsNoTracking() join interest in db.Interests.AsNoTracking() on link.InterestId equals interest.Id where userIds.Contains(link.UserId) select new { link.UserId, interest.Name }).ToListAsync(ct);
        var viewerSkills = await (from link in db.UserSkills.AsNoTracking() join skill in db.Skills.AsNoTracking() on link.SkillId equals skill.Id where link.UserId == viewerId select skill.Name).ToListAsync(ct);
        var viewerInterests = await (from link in db.UserInterests.AsNoTracking() join interest in db.Interests.AsNoTracking() on link.InterestId equals interest.Id where link.UserId == viewerId select interest.Name).ToListAsync(ct);
        var rows = visible.Select(x => {
            var skills = skillRows.Where(s => s.UserId == x.User.Id).Select(s => s.Name).ToList();
            var interests = interestRows.Where(i => i.UserId == x.User.Id).Select(i => i.Name).ToList();
            return new { x, skills, interests, Score = skills.Intersect(viewerSkills, StringComparer.OrdinalIgnoreCase).Count() + interests.Intersect(viewerInterests, StringComparer.OrdinalIgnoreCase).Count() };
        }).Where(x => string.IsNullOrWhiteSpace(f.Skill) || x.skills.Contains(f.Skill, StringComparer.OrdinalIgnoreCase))
          .Where(x => string.IsNullOrWhiteSpace(f.Interest) || x.interests.Contains(f.Interest, StringComparer.OrdinalIgnoreCase))
          .OrderByDescending(x => x.Score).ThenBy(x => x.x.User.LastName).ThenBy(x => x.x.User.FirstName).ToList();
        var page = new PagedRequest(f.PageNumber, f.PageSize);
        var items = rows.Skip(page.Skip).Take(page.SafePageSize).Select(x => new DiscoveredPerson(x.x.User.Id, x.x.User.FirstName,
            x.x.User.LastName, x.x.User.UniversityId, x.x.User.JourneyStage, x.x.Profile.Headline, x.x.Profile.Programme,
            x.x.Profile.StudyArea, x.x.Profile.IndustryExperience, x.x.Profile.CareerGoals, x.x.Profile.ProfileImageUrl, x.skills, x.interests, x.Score)).ToList();
        return new(items, page.SafePageNumber, page.SafePageSize, rows.Count);
    }
}

