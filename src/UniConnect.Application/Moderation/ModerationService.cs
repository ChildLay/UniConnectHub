using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Auth;
using UniConnect.Application.Common;
using UniConnect.Domain.Communities;
using UniConnect.Domain.Content;
using UniConnect.Domain.Moderation;
using UniConnect.Domain.Notifications;
using UniConnect.Domain.Users;

namespace UniConnect.Application.Moderation;

public sealed record CreateReportRequest(string TargetType, Guid TargetId, string Reason, string? Details);
public sealed record ResolveReportRequest(ReportStatus Status, string? Resolution);
public sealed record ReportResponse(Guid Id, Guid ReporterId, string TargetType, Guid TargetId, string Reason, string? Details, ReportStatus Status, Guid? ReviewedByUserId, string? Resolution, DateTime CreatedAtUtc);
public sealed record ManagedUserResponse(Guid UserId, string Email, string FirstName, string LastName, JourneyStage JourneyStage, UserRole Role, bool IsActive, bool EmailVerified, DateTime CreatedAtUtc);

public interface IReportService { Task<ReportResponse> CreateAsync(Guid reporterId, CreateReportRequest request, CancellationToken ct); }
public interface IModerationService
{
    Task<PagedResult<ReportResponse>> ListAsync(ReportStatus? status, PagedRequest page, CancellationToken ct);
    Task<PagedResult<ManagedUserResponse>> ListUsersAsync(string? search, bool? active, PagedRequest page, CancellationToken ct);
    Task<ReportResponse> GetAsync(Guid id, CancellationToken ct);
    Task ResolveAsync(Guid moderatorId, Guid reportId, ResolveReportRequest request, CancellationToken ct);
    Task HideAsync(Guid moderatorId, string targetType, Guid targetId, CancellationToken ct);
    Task SuspendAsync(Guid moderatorId, Guid userId, CancellationToken ct);
    Task SetRoleAsync(Guid moderatorId, Guid userId, UserRole role, CancellationToken ct);
}

public sealed class ReportService(IApplicationDbContext db, IClock clock) : IReportService
{
    public async Task<ReportResponse> CreateAsync(Guid reporterId, CreateReportRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<ReportedTargetType>(request.TargetType, true, out var targetType)) throw new ArgumentException("TargetType must be User, Post, Comment, or Community.");
        var exists = targetType switch
        {
            ReportedTargetType.User => await db.Users.AnyAsync(x => x.Id == request.TargetId, ct),
            ReportedTargetType.Post => await db.Posts.AnyAsync(x => x.Id == request.TargetId, ct),
            ReportedTargetType.Comment => await db.Comments.AnyAsync(x => x.Id == request.TargetId, ct),
            ReportedTargetType.Community => await db.Communities.AnyAsync(x => x.Id == request.TargetId, ct),
            _ => false
        };
        if (!exists) throw new KeyNotFoundException("The item being reported was not found.");
        var entity = new Report(reporterId, targetType.ToString(), request.TargetId, request.Reason, request.Details, clock.UtcNow);
        await db.Reports.AddAsync(entity, ct); await db.SaveChangesAsync(ct); return Map(entity);
    }
    internal static ReportResponse Map(Report x) => new(x.Id, x.ReporterId, x.TargetType, x.TargetId, x.Reason, x.Details, x.Status, x.ReviewedByUserId, x.Resolution, x.CreatedAtUtc);
}

public sealed class ModerationService(IApplicationDbContext db, IClock clock, IAuthStore authStore) : IModerationService
{
    public async Task<PagedResult<ManagedUserResponse>> ListUsersAsync(string? search, bool? active, PagedRequest page, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Email.Contains(term) || x.FirstName.Contains(term) || x.LastName.Contains(term));
        }
        if (active.HasValue) query = query.Where(x => x.IsActive == active.Value);
        var total = await query.LongCountAsync(ct);
        var users = await query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
            .Skip(page.Skip).Take(page.SafePageSize).ToListAsync(ct);
        var items = users.Select(x => new ManagedUserResponse(x.Id, x.Email, x.FirstName, x.LastName,
            x.JourneyStage, x.Role, x.IsActive, x.EmailVerified, x.CreatedAtUtc)).ToList();
        return new(items, page.SafePageNumber, page.SafePageSize, total);
    }
    public async Task<PagedResult<ReportResponse>> ListAsync(ReportStatus? status, PagedRequest page, CancellationToken ct)
    {
        var query = db.Reports.AsNoTracking(); if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        var total = await query.LongCountAsync(ct); var items = await query.OrderBy(x => x.CreatedAtUtc).Skip(page.Skip).Take(page.SafePageSize).ToListAsync(ct);
        return new(items.Select(ReportService.Map).ToList(), page.SafePageNumber, page.SafePageSize, total);
    }
    public async Task<ReportResponse> GetAsync(Guid id, CancellationToken ct) => ReportService.Map(await db.Reports.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Report was not found."));
    public async Task ResolveAsync(Guid moderatorId, Guid reportId, ResolveReportRequest request, CancellationToken ct)
    {
        var report = await db.Reports.SingleOrDefaultAsync(x => x.Id == reportId, ct) ?? throw new KeyNotFoundException("Report was not found.");
        if (request.Status is not (ReportStatus.Resolved or ReportStatus.Dismissed or ReportStatus.UnderReview)) throw new ArgumentException("Status must be UnderReview, Resolved, or Dismissed.");
        report.Review(moderatorId, request.Status, request.Resolution, clock.UtcNow);
        await Log(moderatorId, $"Report{request.Status}", "Report", reportId, request.Resolution, ct);
    }
    public async Task HideAsync(Guid moderatorId, string targetType, Guid targetId, CancellationToken ct)
    {
        var type = targetType.Trim().ToLowerInvariant();
        switch (type)
        {
            case "post": var post = await db.Posts.SingleOrDefaultAsync(x => x.Id == targetId, ct) ?? throw new KeyNotFoundException("Post was not found."); post.Hide(clock.UtcNow); break;
            case "comment": var comment = await db.Comments.SingleOrDefaultAsync(x => x.Id == targetId, ct) ?? throw new KeyNotFoundException("Comment was not found."); comment.Hide(clock.UtcNow); break;
            case "community": var community = await db.Communities.SingleOrDefaultAsync(x => x.Id == targetId, ct) ?? throw new KeyNotFoundException("Community was not found."); community.Hide(clock.UtcNow); break;
            case "notification": var notification = await db.Notifications.SingleOrDefaultAsync(x => x.Id == targetId, ct) ?? throw new KeyNotFoundException("Notification was not found."); notification.Hide(clock.UtcNow); break;
            default: throw new ArgumentException("Only posts, comments, communities, or notifications can be hidden.");
        }
        await Log(moderatorId, "ContentHidden", type, targetId, null, ct);
    }
    public async Task SuspendAsync(Guid moderatorId, Guid userId, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct) ?? throw new KeyNotFoundException("User was not found.");
        user.Deactivate(clock.UtcNow); await Log(moderatorId, "UserSuspended", "User", userId, null, ct);
    }
    public async Task SetRoleAsync(Guid moderatorId, Guid userId, UserRole role, CancellationToken ct)
    {
        if (!Enum.IsDefined(role)) throw new ArgumentException("Role is invalid.");
        if (moderatorId == userId) throw new InvalidOperationException("You can’t change your own access role.");
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct) ?? throw new KeyNotFoundException("User was not found.");
        if (user.Role == UserRole.Admin && role != UserRole.Admin &&
            await db.Users.CountAsync(x => x.IsActive && x.Role == UserRole.Admin, ct) <= 1)
            throw new InvalidOperationException("The last active administrator can’t be demoted.");
        user.SetRole(role, clock.UtcNow);
        await authStore.RevokeAllRefreshTokensAsync(userId, clock.UtcNow, ct);
        await Log(moderatorId, "UserRoleChanged", "User", userId, role.ToString(), ct);
    }
    private async Task Log(Guid moderatorId, string action, string type, Guid targetId, string? details, CancellationToken ct)
    {
        var now = clock.UtcNow;
        await db.AuditLogs.AddAsync(new AuditLog(moderatorId, action, type, targetId, details, now), ct);
        await db.ModerationActions.AddAsync(new ModerationAction(moderatorId, action, type, targetId, details, now), ct);
        await db.SaveChangesAsync(ct);
    }
}
