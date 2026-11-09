using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Common;
using UniConnect.Domain.Universities;

namespace UniConnect.Application.Universities;

public sealed record UniversityResponse(Guid Id, string Name, string? ShortName, string Country, string? City, string? WebsiteUrl);

public interface IUniversityService
{
    Task<PagedResult<UniversityResponse>> ListAsync(PagedRequest page, string? search, CancellationToken cancellationToken);
    Task<UniversityResponse> GetAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class UniversityService(IApplicationDbContext db) : IUniversityService
{
    public async Task<PagedResult<UniversityResponse>> ListAsync(PagedRequest page, string? search, CancellationToken cancellationToken)
    {
        var query = db.Universities.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search) || x.Country.Contains(search));
        var total = await query.LongCountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Name).Skip(page.Skip).Take(page.SafePageSize)
            .Select(x => new UniversityResponse(x.Id, x.Name, x.ShortName, x.Country, x.City, x.WebsiteUrl)).ToListAsync(cancellationToken);
        return new(items, page.SafePageNumber, page.SafePageSize, total);
    }

    public async Task<UniversityResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.Universities.AsNoTracking().Where(x => x.Id == id && x.IsActive)
            .Select(x => new UniversityResponse(x.Id, x.Name, x.ShortName, x.Country, x.City, x.WebsiteUrl))
            .SingleOrDefaultAsync(cancellationToken) ?? throw new KeyNotFoundException("University was not found.");
    }
}
