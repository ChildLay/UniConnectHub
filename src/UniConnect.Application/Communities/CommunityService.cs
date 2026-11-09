using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Common;
using UniConnect.Domain.Communities;

namespace UniConnect.Application.Communities;

public sealed record CommunityResponse(Guid Id, string Name, string? Description, Guid UniversityId, Guid OwnerId,
    CommunityVisibility Visibility, int MemberCount, DateTime CreatedAtUtc);
public sealed record CommunityInput(string Name, string? Description, Guid UniversityId, CommunityVisibility Visibility);

public interface ICommunityService
{
    Task<CommunityResponse> CreateAsync(Guid userId, CommunityInput input, CancellationToken cancellationToken);
    Task<PagedResult<CommunityResponse>> ListAsync(Guid userId, PagedRequest page, CancellationToken cancellationToken);
    Task<CommunityResponse> GetAsync(Guid userId, Guid communityId, CancellationToken cancellationToken);
    Task<CommunityResponse> UpdateAsync(Guid userId, Guid communityId, CommunityInput input, CancellationToken cancellationToken);
    Task DeleteAsync(Guid userId, Guid communityId, CancellationToken cancellationToken);
    Task JoinAsync(Guid userId, Guid communityId, CancellationToken cancellationToken);
    Task LeaveAsync(Guid userId, Guid communityId, CancellationToken cancellationToken);
    Task<PagedResult<Guid>> MembersAsync(Guid userId, Guid communityId, PagedRequest page, CancellationToken cancellationToken);
}

public sealed class CommunityService(IApplicationDbContext db, IClock clock) : ICommunityService
{
    public async Task<CommunityResponse> CreateAsync(Guid userId, CommunityInput input, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(input.Visibility)) throw new ArgumentException("Community visibility is invalid.");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken) ?? throw new UnauthorizedAccessException();
        if (user.UniversityId != input.UniversityId) throw new AccessDeniedException("Communities can only be created for your university.");
        var entity = new Community(input.Name, input.Description, input.UniversityId, userId, input.Visibility, clock.UtcNow);
        await db.Communities.AddAsync(entity, cancellationToken);
        await db.CommunityMembers.AddAsync(new CommunityMember(entity.Id, userId, CommunityMemberRole.Admin, clock.UtcNow), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, entity.Id, cancellationToken);
    }

    public async Task<PagedResult<CommunityResponse>> ListAsync(Guid userId, PagedRequest page, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken) ?? throw new UnauthorizedAccessException();
        var query = db.Communities.AsNoTracking().Where(c => c.IsActive && !c.IsHidden && c.UniversityId == user.UniversityId &&
            (c.Visibility == CommunityVisibility.Public || db.CommunityMembers.Any(m => m.CommunityId == c.Id && m.UserId == userId)));
        var total = await query.LongCountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Name).Skip(page.Skip).Take(page.SafePageSize).ToListAsync(cancellationToken);
        var ids = items.Select(x => x.Id).ToArray();
        var counts = await db.CommunityMembers.AsNoTracking().Where(x => ids.Contains(x.CommunityId)).GroupBy(x => x.CommunityId)
            .Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);
        return new(items.Select(x => Map(x, counts.GetValueOrDefault(x.Id))).ToList(), page.SafePageNumber, page.SafePageSize, total);
    }

    public async Task<CommunityResponse> GetAsync(Guid userId, Guid communityId, CancellationToken cancellationToken)
    {
        var entity = await db.Communities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == communityId && x.IsActive && !x.IsHidden, cancellationToken) ?? throw new KeyNotFoundException("Community was not found.");
        var member = await db.CommunityMembers.AnyAsync(x => x.CommunityId == communityId && x.UserId == userId, cancellationToken);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken);
        if (!member && (entity.Visibility != CommunityVisibility.Public || user?.UniversityId != entity.UniversityId)) throw new AccessDeniedException("You cannot view this community.");
        var count = await db.CommunityMembers.CountAsync(x => x.CommunityId == communityId, cancellationToken);
        return Map(entity, count);
    }

    public async Task<CommunityResponse> UpdateAsync(Guid userId, Guid communityId, CommunityInput input, CancellationToken cancellationToken)
    {
        var entity = await db.Communities.SingleOrDefaultAsync(x => x.Id == communityId && x.IsActive, cancellationToken) ?? throw new KeyNotFoundException("Community was not found.");
        await EnsureAdmin(userId, communityId, entity.OwnerId, cancellationToken);
        if (input.UniversityId != entity.UniversityId) throw new ArgumentException("Community university cannot be changed.");
        entity.Update(input.Name, input.Description, input.Visibility, clock.UtcNow); await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, communityId, cancellationToken);
    }

    public async Task DeleteAsync(Guid userId, Guid communityId, CancellationToken cancellationToken)
    {
        var entity = await db.Communities.SingleOrDefaultAsync(x => x.Id == communityId && x.IsActive, cancellationToken) ?? throw new KeyNotFoundException("Community was not found.");
        await EnsureAdmin(userId, communityId, entity.OwnerId, cancellationToken); entity.Deactivate(clock.UtcNow); await db.SaveChangesAsync(cancellationToken);
    }

    public async Task JoinAsync(Guid userId, Guid communityId, CancellationToken cancellationToken)
    {
        var community = await db.Communities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == communityId && x.IsActive && !x.IsHidden, cancellationToken) ?? throw new KeyNotFoundException("Community was not found.");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken) ?? throw new UnauthorizedAccessException();
        if (community.UniversityId != user.UniversityId) throw new AccessDeniedException("You cannot join a community at another university.");
        if (community.Visibility == CommunityVisibility.Private) throw new AccessDeniedException("This private community requires an invitation.");
        if (!await db.CommunityMembers.AnyAsync(x => x.CommunityId == communityId && x.UserId == userId, cancellationToken))
        { await db.CommunityMembers.AddAsync(new CommunityMember(communityId, userId, CommunityMemberRole.Member, clock.UtcNow), cancellationToken); await db.SaveChangesAsync(cancellationToken); }
    }

    public async Task LeaveAsync(Guid userId, Guid communityId, CancellationToken cancellationToken)
    {
        var membership = await db.CommunityMembers.SingleOrDefaultAsync(x => x.CommunityId == communityId && x.UserId == userId, cancellationToken);
        if (membership is null) return;
        var isOwner = await db.Communities.AnyAsync(x => x.Id == communityId && x.OwnerId == userId && x.IsActive, cancellationToken);
        if (isOwner) throw new InvalidOperationException("The community administrator cannot leave before assigning another administrator.");
        db.CommunityMembers.Remove(membership); await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<Guid>> MembersAsync(Guid userId, Guid communityId, PagedRequest page, CancellationToken cancellationToken)
    {
        _ = await GetAsync(userId, communityId, cancellationToken);
        var query = db.CommunityMembers.AsNoTracking().Where(x => x.CommunityId == communityId).Select(x => x.UserId);
        var total = await query.LongCountAsync(cancellationToken); var items = await query.OrderBy(x => x).Skip(page.Skip).Take(page.SafePageSize).ToListAsync(cancellationToken);
        return new(items, page.SafePageNumber, page.SafePageSize, total);
    }

    private async Task EnsureAdmin(Guid userId, Guid communityId, Guid ownerId, CancellationToken ct)
    {
        if (userId != ownerId && !await db.CommunityMembers.AnyAsync(x => x.CommunityId == communityId && x.UserId == userId && x.Role == CommunityMemberRole.Admin, ct))
            throw new AccessDeniedException("Community administrator permission is required.");
    }
    private static CommunityResponse Map(Community x, int count) => new(x.Id, x.Name, x.Description, x.UniversityId, x.OwnerId, x.Visibility, count, x.CreatedAtUtc);
}
