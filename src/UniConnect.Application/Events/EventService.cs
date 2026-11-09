using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Common;
using UniConnect.Domain.Events;

namespace UniConnect.Application.Events;

public sealed record EventResponse(Guid Id, string Title, string? Description, Guid HostId, Guid UniversityId,
    Guid? CommunityId, DateTime StartUtc, DateTime EndUtc, int? Capacity, EventType Type, EventStatus Status, int RegistrationCount);
public sealed record CreateEventRequest(string Title, string? Description, Guid? CommunityId, DateTime StartUtc,
    DateTime EndUtc, int? Capacity, EventType Type);
public sealed record UpdateEventRequest(string Title, string? Description, DateTime StartUtc, DateTime EndUtc, int? Capacity, EventType Type);

public interface IEventService
{
    Task<EventResponse> CreateAsync(Guid userId, CreateEventRequest request, CancellationToken cancellationToken);
    Task<PagedResult<EventResponse>> ListAsync(Guid userId, DateTime? fromUtc, DateTime? toUtc, PagedRequest page, CancellationToken cancellationToken);
    Task<EventResponse> GetAsync(Guid userId, Guid eventId, CancellationToken cancellationToken);
    Task<EventResponse> UpdateAsync(Guid userId, Guid eventId, UpdateEventRequest request, CancellationToken cancellationToken);
    Task RegisterAsync(Guid userId, Guid eventId, CancellationToken cancellationToken);
    Task UnregisterAsync(Guid userId, Guid eventId, CancellationToken cancellationToken);
    Task CancelAsync(Guid userId, Guid eventId, CancellationToken cancellationToken);
    Task<PagedResult<Guid>> AttendeesAsync(Guid userId, Guid eventId, PagedRequest page, CancellationToken cancellationToken);
}

public sealed class EventService(IApplicationDbContext db, IClock clock) : IEventService
{
    public async Task<EventResponse> CreateAsync(Guid userId, CreateEventRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken) ?? throw new UnauthorizedAccessException();
        if (!Enum.IsDefined(request.Type)) throw new ArgumentException("Event type is invalid.");
        var entity = new Domain.Events.Event(request.Title, request.Description, userId, user.UniversityId, request.StartUtc,
            request.EndUtc, request.Capacity, request.Type, clock.UtcNow, request.CommunityId);
        entity.Publish(clock.UtcNow);
        await db.Events.AddAsync(entity, cancellationToken); await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, entity.Id, cancellationToken);
    }

    public async Task<PagedResult<EventResponse>> ListAsync(Guid userId, DateTime? fromUtc, DateTime? toUtc, PagedRequest page, CancellationToken cancellationToken)
    {
        if (fromUtc.HasValue && toUtc.HasValue && toUtc < fromUtc) throw new ArgumentException("End date must be after start date.");
        var query = db.Events.AsNoTracking().Where(x => x.Status == EventStatus.Published);
        if (fromUtc.HasValue) query = query.Where(x => x.StartUtc >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(x => x.StartUtc <= toUtc.Value);
        var total = await query.LongCountAsync(cancellationToken);
        var events = await query.OrderBy(x => x.StartUtc).Skip(page.Skip).Take(page.SafePageSize).ToListAsync(cancellationToken);
        var ids = events.Select(x => x.Id).ToArray();
        var counts = await db.EventRegistrations.AsNoTracking().Where(x => ids.Contains(x.EventId) && x.Status == EventRegistrationStatus.Registered)
            .GroupBy(x => x.EventId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);
        var items = events.Select(x => ToResponse(x, counts.GetValueOrDefault(x.Id))).ToList();
        return new(items, page.SafePageNumber, page.SafePageSize, total);
    }

    public async Task<EventResponse> GetAsync(Guid userId, Guid eventId, CancellationToken cancellationToken)
    {
        var entity = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId, cancellationToken) ?? throw new KeyNotFoundException("Event was not found.");
        if (entity.Status != EventStatus.Published && entity.HostId != userId) throw new KeyNotFoundException("Event was not found.");
        var count = await db.EventRegistrations.CountAsync(x => x.EventId == eventId && x.Status == EventRegistrationStatus.Registered, cancellationToken);
        return ToResponse(entity, count);
    }

    public async Task<EventResponse> UpdateAsync(Guid userId, Guid eventId, UpdateEventRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Type)) throw new ArgumentException("Event type is invalid.");
        var entity = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId, cancellationToken) ?? throw new KeyNotFoundException("Event was not found.");
        if (entity.HostId != userId) throw new AccessDeniedException("Only the event organiser can edit it.");
        entity.Update(request.Title, request.Description, request.StartUtc, request.EndUtc, request.Capacity, request.Type, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken); return await GetAsync(userId, eventId, cancellationToken);
    }

    public async Task RegisterAsync(Guid userId, Guid eventId, CancellationToken cancellationToken)
    {
        var entity = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId, cancellationToken) ?? throw new KeyNotFoundException("Event was not found.");
        if (entity.Status != EventStatus.Published) throw new InvalidOperationException("Event is not open for registration.");
        if (await db.EventRegistrations.AnyAsync(x => x.EventId == eventId && x.UserId == userId, cancellationToken)) throw new InvalidOperationException("User is already registered.");
        var count = await db.EventRegistrations.CountAsync(x => x.EventId == eventId && x.Status == EventRegistrationStatus.Registered, cancellationToken);
        if (entity.Capacity is { } capacity && count >= capacity) throw new InvalidOperationException("Event capacity has been reached.");
        await db.EventRegistrations.AddAsync(new EventRegistration(eventId, userId, EventRegistrationStatus.Registered, clock.UtcNow), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnregisterAsync(Guid userId, Guid eventId, CancellationToken cancellationToken)
    {
        var registration = await db.EventRegistrations.SingleOrDefaultAsync(x => x.EventId == eventId && x.UserId == userId, cancellationToken);
        if (registration is not null) { db.EventRegistrations.Remove(registration); await db.SaveChangesAsync(cancellationToken); }
    }

    public async Task CancelAsync(Guid userId, Guid eventId, CancellationToken cancellationToken)
    {
        var entity = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId, cancellationToken) ?? throw new KeyNotFoundException("Event was not found.");
        if (entity.HostId != userId) throw new AccessDeniedException("Only the event organiser can cancel it.");
        entity.Cancel(clock.UtcNow); await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<Guid>> AttendeesAsync(Guid userId, Guid eventId, PagedRequest page, CancellationToken cancellationToken)
    {
        var entity = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId, cancellationToken) ?? throw new KeyNotFoundException("Event was not found.");
        if (entity.HostId != userId) throw new AccessDeniedException("Only the organiser can view the attendee list.");
        var query = db.EventRegistrations.AsNoTracking().Where(x => x.EventId == eventId && x.Status == EventRegistrationStatus.Registered).Select(x => x.UserId);
        var total = await query.LongCountAsync(cancellationToken); var items = await query.OrderBy(x => x).Skip(page.Skip).Take(page.SafePageSize).ToListAsync(cancellationToken);
        return new(items, page.SafePageNumber, page.SafePageSize, total);
    }

    private static EventResponse ToResponse(Domain.Events.Event e, int count) => new(e.Id, e.Title, e.Description, e.HostId, e.UniversityId, e.CommunityId, e.StartUtc, e.EndUtc, e.Capacity, e.Type, e.Status, count);
}
