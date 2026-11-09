using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Common;
using UniConnect.Domain.Notifications;

namespace UniConnect.Application.Notifications;

public sealed record NotificationResponse(Guid Id, string Type, string Title, string Message, string? ResourceType, Guid? ResourceId, bool IsRead, DateTime CreatedAtUtc);
public interface INotificationService
{
    Task CreateAsync(Guid userId, string type, string title, string message, string? resourceType, Guid? resourceId, CancellationToken ct);
    Task<PagedResult<NotificationResponse>> ListAsync(Guid userId, PagedRequest page, CancellationToken ct);
    Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct);
    Task MarkAllReadAsync(Guid userId, CancellationToken ct);
}

public sealed class NotificationService(IApplicationDbContext db, IClock clock) : INotificationService
{
    public async Task CreateAsync(Guid userId, string type, string title, string message, string? resourceType, Guid? resourceId, CancellationToken ct)
    {
        await db.Notifications.AddAsync(new Notification(userId, type, title, message, resourceType, resourceId, clock.UtcNow), ct);
        await db.SaveChangesAsync(ct);
    }
    public async Task<PagedResult<NotificationResponse>> ListAsync(Guid userId, PagedRequest page, CancellationToken ct)
    {
        var query = db.Notifications.AsNoTracking().Where(x => x.UserId == userId && !x.IsHidden);
        var total = await query.LongCountAsync(ct); var items = await query.OrderByDescending(x => x.CreatedAtUtc).Skip(page.Skip).Take(page.SafePageSize)
            .Select(x => new NotificationResponse(x.Id, x.Type, x.Title, x.Message, x.ResourceType, x.ResourceId, x.IsRead, x.CreatedAtUtc)).ToListAsync(ct);
        return new(items, page.SafePageNumber, page.SafePageSize, total);
    }
    public async Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct)
    {
        var item = await db.Notifications.SingleOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId, ct) ?? throw new KeyNotFoundException("Notification was not found.");
        item.MarkRead(clock.UtcNow); await db.SaveChangesAsync(ct);
    }
    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct)
    {
        var items = await db.Notifications.Where(x => x.UserId == userId && !x.IsRead && !x.IsHidden).ToListAsync(ct);
        foreach (var item in items) item.MarkRead(clock.UtcNow);
        if (items.Count > 0) await db.SaveChangesAsync(ct);
    }
}
