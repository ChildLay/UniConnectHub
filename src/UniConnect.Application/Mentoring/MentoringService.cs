using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Common;
using UniConnect.Domain.Mentoring;
using UniConnect.Domain.Users;

namespace UniConnect.Application.Mentoring;

public static class MentorEligibility
{
    public static void EnsureAlumni(JourneyStage journeyStage)
    {
        if (journeyStage != JourneyStage.Alumni)
            throw new AccessDeniedException("Only alumni can create or update a mentor profile.");
    }

    public static void EnsureNotSelf(Guid menteeId, Guid mentorId)
    {
        if (menteeId == mentorId)
            throw new ArgumentException("A user cannot mentor themselves.");
    }
}

public sealed record MentorProfileRequest(string Bio, int? Capacity, bool IsAvailable = true);
public sealed record MentorResponse(Guid UserId, string FirstName, string LastName, string Bio, int? Capacity, bool IsAvailable, int ActiveMenteeCount, string? StudyArea, int? GraduationYear);
public sealed record MentorshipRequestInput(Guid MentorId, string Message);
public sealed record MentorshipResponse(Guid Id, Guid MentorId, Guid MenteeId, string Message, MentorshipStatus Status, DateTime CreatedAtUtc);

public interface IMentoringService
{
    Task<MentorResponse> SetProfileAsync(Guid userId, MentorProfileRequest request, CancellationToken ct);
    Task<PagedResult<MentorResponse>> ListMentorsAsync(Guid viewerId, PagedRequest page, CancellationToken ct);
    Task<MentorResponse> GetMentorAsync(Guid userId, CancellationToken ct);
    Task<MentorshipResponse> RequestAsync(Guid menteeId, MentorshipRequestInput request, CancellationToken ct);
    Task<PagedResult<MentorshipResponse>> ListRequestsAsync(Guid userId, PagedRequest page, CancellationToken ct);
    Task TransitionAsync(Guid userId, Guid requestId, MentorshipStatus status, CancellationToken ct);
    Task<PagedResult<MentorshipResponse>> RelationshipsAsync(Guid userId, PagedRequest page, CancellationToken ct);
}

public sealed class MentoringService(IApplicationDbContext db, IClock clock, UniConnect.Application.Notifications.INotificationService notifications) : IMentoringService
{
    public async Task<MentorResponse> SetProfileAsync(Guid userId, MentorProfileRequest request, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, ct) ?? throw new UnauthorizedAccessException();
        MentorEligibility.EnsureAlumni(user.JourneyStage);
        var profile = await db.MentorProfiles.SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (profile is null) { profile = new MentorProfile(userId, request.Bio, request.Capacity, clock.UtcNow); await db.MentorProfiles.AddAsync(profile, ct); }
        var active = await db.MentorshipRequests.CountAsync(x => x.MentorId == userId && x.Status == MentorshipStatus.Accepted, ct);
        profile.Update(request.Bio, request.Capacity, request.IsAvailable, active, clock.UtcNow);
        await db.SaveChangesAsync(ct); return await GetMentorAsync(userId, ct);
    }
    public async Task<PagedResult<MentorResponse>> ListMentorsAsync(Guid viewerId, PagedRequest page, CancellationToken ct)
    {
        var query = from m in db.MentorProfiles.AsNoTracking()
                    join u in db.Users.AsNoTracking() on m.Id equals u.Id
                    join p in db.Profiles.AsNoTracking() on u.Id equals p.Id into profiles
                    from p in profiles.DefaultIfEmpty()
                    where m.Id != viewerId && m.IsAvailable && u.IsActive && u.JourneyStage == JourneyStage.Alumni
                    select new { m, u, p };
        var total = await query.LongCountAsync(ct); var rows = await query.OrderBy(x => x.u.LastName).Skip(page.Skip).Take(page.SafePageSize).ToListAsync(ct);
        return new(rows.Select(x => Map(x.u, x.m,
            x.p?.Visibility == UniConnect.Domain.Profiles.ProfileVisibility.UniversityCommunity ? x.p.StudyArea : null,
            x.p?.Visibility == UniConnect.Domain.Profiles.ProfileVisibility.UniversityCommunity ? x.p.GraduationYear : null)).ToList(), page.SafePageNumber, page.SafePageSize, total);
    }
    public async Task<MentorResponse> GetMentorAsync(Guid userId, CancellationToken ct)
    {
        var result = await (from m in db.MentorProfiles.AsNoTracking()
                            join u in db.Users.AsNoTracking() on m.Id equals u.Id
                            join p in db.Profiles.AsNoTracking() on u.Id equals p.Id into profiles
                            from p in profiles.DefaultIfEmpty()
                            where m.Id == userId && u.IsActive && u.JourneyStage == JourneyStage.Alumni
                            select new { m, u, p }).SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Mentor profile was not found.");
        return Map(result.u, result.m,
            result.p?.Visibility == UniConnect.Domain.Profiles.ProfileVisibility.UniversityCommunity ? result.p.StudyArea : null,
            result.p?.Visibility == UniConnect.Domain.Profiles.ProfileVisibility.UniversityCommunity ? result.p.GraduationYear : null);
    }
    public async Task<MentorshipResponse> RequestAsync(Guid menteeId, MentorshipRequestInput request, CancellationToken ct)
    {
        MentorEligibility.EnsureNotSelf(menteeId, request.MentorId);
        var mentor = await db.MentorProfiles.SingleOrDefaultAsync(x => x.Id == request.MentorId, ct) ?? throw new KeyNotFoundException("Mentor profile was not found.");
        var mentorUser = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.MentorId && x.IsActive, ct) ?? throw new KeyNotFoundException("Mentor profile was not found.");
        MentorEligibility.EnsureAlumni(mentorUser.JourneyStage);
        if (!mentor.IsAvailable) throw new InvalidOperationException("This mentor is not available.");
        var existing = await db.MentorshipRequests.AnyAsync(x => x.MentorId == request.MentorId && x.MenteeId == menteeId && (x.Status == MentorshipStatus.Pending || x.Status == MentorshipStatus.Accepted), ct);
        if (existing) throw new InvalidOperationException("An active mentorship request already exists.");
        var activeCount = await db.MentorshipRequests.CountAsync(x => x.MentorId == request.MentorId && x.Status == MentorshipStatus.Accepted, ct);
        if (mentor.Capacity is { } capacity && activeCount >= capacity) throw new InvalidOperationException("The mentor has reached capacity.");
        var entity = new MentorshipRequest(request.MentorId, menteeId, request.Message, clock.UtcNow); await db.MentorshipRequests.AddAsync(entity, ct); await db.SaveChangesAsync(ct);
        await notifications.CreateAsync(request.MentorId, "MentorshipRequest", "New mentoring request", "You received a mentoring request.", "MentorshipRequest", entity.Id, ct); return Map(entity);
    }
    public async Task<PagedResult<MentorshipResponse>> ListRequestsAsync(Guid userId, PagedRequest page, CancellationToken ct)
    {
        var query = db.MentorshipRequests.AsNoTracking().Where(x => x.MentorId == userId || x.MenteeId == userId);
        var total = await query.LongCountAsync(ct); var rows = await query.OrderByDescending(x => x.CreatedAtUtc).Skip(page.Skip).Take(page.SafePageSize).ToListAsync(ct);
        return new(rows.Select(Map).ToList(), page.SafePageNumber, page.SafePageSize, total);
    }
    public async Task TransitionAsync(Guid userId, Guid requestId, MentorshipStatus status, CancellationToken ct)
    {
        var item = await db.MentorshipRequests.SingleOrDefaultAsync(x => x.Id == requestId, ct) ?? throw new KeyNotFoundException("Mentorship request was not found.");
        if (status is MentorshipStatus.Accepted or MentorshipStatus.Declined)
        {
            if (item.MentorId != userId) throw new AccessDeniedException("Only the mentor can respond to a mentorship request.");
            if (status == MentorshipStatus.Accepted)
            {
                var mentor = await db.MentorProfiles.SingleAsync(x => x.Id == item.MentorId, ct);
                var active = await db.MentorshipRequests.CountAsync(x => x.MentorId == item.MentorId && x.Status == MentorshipStatus.Accepted, ct);
                if (!mentor.IsAvailable || mentor.Capacity is { } capacity && active >= capacity) throw new InvalidOperationException("The mentor is unavailable or has reached capacity.");
                mentor.SetActiveMenteeCount(active + 1, clock.UtcNow);
            }
        }
        else if (status is MentorshipStatus.Completed or MentorshipStatus.Cancelled)
        {
            if (item.MentorId != userId && item.MenteeId != userId) throw new AccessDeniedException("You are not part of this mentorship.");
            if (item.Status == MentorshipStatus.Accepted && status == MentorshipStatus.Completed)
            {
                var mentor = await db.MentorProfiles.SingleAsync(x => x.Id == item.MentorId, ct);
                mentor.SetActiveMenteeCount(Math.Max(0, mentor.ActiveMenteeCount - 1), clock.UtcNow);
            }
        }
        else throw new ArgumentException("Unsupported mentorship transition.");
        item.Transition(status, clock.UtcNow); await db.SaveChangesAsync(ct);
        if (status == MentorshipStatus.Accepted) await notifications.CreateAsync(item.MenteeId, "MentorshipAccepted", "Mentoring request accepted", "Your mentoring request was accepted.", "MentorshipRequest", item.Id, ct);
    }
    public async Task<PagedResult<MentorshipResponse>> RelationshipsAsync(Guid userId, PagedRequest page, CancellationToken ct)
    {
        var query = db.MentorshipRequests.AsNoTracking().Where(x => (x.MentorId == userId || x.MenteeId == userId) && x.Status == MentorshipStatus.Accepted);
        var total = await query.LongCountAsync(ct); var rows = await query.OrderByDescending(x => x.UpdatedAtUtc).Skip(page.Skip).Take(page.SafePageSize).ToListAsync(ct);
        return new(rows.Select(Map).ToList(), page.SafePageNumber, page.SafePageSize, total);
    }
    private static MentorResponse Map(UniConnect.Domain.Users.User u, MentorProfile m, string? studyArea, int? graduationYear) => new(u.Id, u.FirstName, u.LastName, m.Bio, m.Capacity, m.IsAvailable, m.ActiveMenteeCount, studyArea, graduationYear);
    private static MentorshipResponse Map(MentorshipRequest x) => new(x.Id, x.MentorId, x.MenteeId, x.Message, x.Status, x.CreatedAtUtc);
}
