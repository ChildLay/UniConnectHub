using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Common;
using UniConnect.Domain.Opportunities;

namespace UniConnect.Application.Opportunities;

public sealed record OpportunityResponse(Guid Id, string Title, string Description, Guid CreatorId, Guid UniversityId,
    OpportunityType Type, DateTime? ClosingDateUtc, string? ApplicationUrl, DateTime CreatedAtUtc);
public sealed record OpportunityInput(string Title, string Description, OpportunityType Type, DateTime? ClosingDateUtc, string? ApplicationUrl);

public interface IOpportunityService
{
    Task<OpportunityResponse> CreateAsync(Guid userId, OpportunityInput input, CancellationToken cancellationToken);
    Task<PagedResult<OpportunityResponse>> ListAsync(Guid userId, OpportunityType? type, PagedRequest page, CancellationToken cancellationToken);
    Task<OpportunityResponse> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken);
    Task<OpportunityResponse> UpdateAsync(Guid userId, Guid id, OpportunityInput input, CancellationToken cancellationToken);
    Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken);
    Task SaveAsync(Guid userId, Guid id, CancellationToken cancellationToken);
    Task UnsaveAsync(Guid userId, Guid id, CancellationToken cancellationToken);
    Task<PagedResult<OpportunityResponse>> SavedAsync(Guid userId, PagedRequest page, CancellationToken cancellationToken);
}

public sealed class OpportunityService(IApplicationDbContext db, IClock clock) : IOpportunityService
{
    public async Task<OpportunityResponse> CreateAsync(Guid userId, OpportunityInput input, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(input.Type)) throw new ArgumentException("Opportunity type is invalid.");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken) ?? throw new UnauthorizedAccessException();
        var entity = new Opportunity(input.Title, input.Description, userId, user.UniversityId, input.Type, input.ClosingDateUtc, input.ApplicationUrl, clock.UtcNow);
        await db.Opportunities.AddAsync(entity, cancellationToken); await db.SaveChangesAsync(cancellationToken); return Map(entity);
    }
    public async Task<PagedResult<OpportunityResponse>> ListAsync(Guid userId, OpportunityType? type, PagedRequest page, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken) ?? throw new UnauthorizedAccessException();
        var query = db.Opportunities.AsNoTracking().Where(x => x.IsActive && x.UniversityId == user.UniversityId && (x.ClosingDateUtc == null || x.ClosingDateUtc > clock.UtcNow));
        if (type.HasValue) query = query.Where(x => x.Type == type.Value);
        var total = await query.LongCountAsync(cancellationToken); var items = await query.OrderByDescending(x => x.CreatedAtUtc).Skip(page.Skip).Take(page.SafePageSize).ToListAsync(cancellationToken);
        return new(items.Select(Map).ToList(), page.SafePageNumber, page.SafePageSize, total);
    }
    public async Task<OpportunityResponse> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken) ?? throw new UnauthorizedAccessException();
        var item = await db.Opportunities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsActive && x.UniversityId == user.UniversityId, cancellationToken) ?? throw new KeyNotFoundException("Opportunity was not found.");
        return Map(item);
    }
    public async Task<OpportunityResponse> UpdateAsync(Guid userId, Guid id, OpportunityInput input, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(input.Type)) throw new ArgumentException("Opportunity type is invalid.");
        var item = await db.Opportunities.SingleOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken) ?? throw new KeyNotFoundException("Opportunity was not found.");
        if (item.CreatorId != userId) throw new AccessDeniedException("Only the opportunity creator can edit it.");
        item.Update(input.Title, input.Description, input.Type, input.ClosingDateUtc, input.ApplicationUrl, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken); return Map(item);
    }
    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var item = await db.Opportunities.SingleOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken) ?? throw new KeyNotFoundException("Opportunity was not found.");
        if (item.CreatorId != userId) throw new AccessDeniedException("Only the opportunity creator can delete it.");
        item.Close(clock.UtcNow); await db.SaveChangesAsync(cancellationToken);
    }
    public async Task SaveAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        _ = await GetAsync(userId, id, cancellationToken);
        if (!await db.SavedOpportunities.AnyAsync(x => x.UserId == userId && x.OpportunityId == id, cancellationToken))
        { await db.SavedOpportunities.AddAsync(new SavedOpportunity(id, userId, clock.UtcNow), cancellationToken); await db.SaveChangesAsync(cancellationToken); }
    }
    public async Task UnsaveAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var link = await db.SavedOpportunities.SingleOrDefaultAsync(x => x.UserId == userId && x.OpportunityId == id, cancellationToken);
        if (link is not null) { db.SavedOpportunities.Remove(link); await db.SaveChangesAsync(cancellationToken); }
    }
    public async Task<PagedResult<OpportunityResponse>> SavedAsync(Guid userId, PagedRequest page, CancellationToken cancellationToken)
    {
        var query = from saved in db.SavedOpportunities.AsNoTracking() join opportunity in db.Opportunities.AsNoTracking() on saved.OpportunityId equals opportunity.Id where saved.UserId == userId && opportunity.IsActive select opportunity;
        var total = await query.LongCountAsync(cancellationToken); var items = await query.OrderByDescending(x => x.CreatedAtUtc).Skip(page.Skip).Take(page.SafePageSize).ToListAsync(cancellationToken);
        return new(items.Select(Map).ToList(), page.SafePageNumber, page.SafePageSize, total);
    }
    private static OpportunityResponse Map(UniConnect.Domain.Opportunities.Opportunity x) => new(x.Id, x.Title, x.Description, x.CreatorId, x.UniversityId, x.Type, x.ClosingDateUtc, x.ApplicationUrl, x.CreatedAtUtc);
}
