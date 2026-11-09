using Microsoft.EntityFrameworkCore;
using UniConnect.Application.Common;
using UniConnect.Domain.Connections;

namespace UniConnect.Application.Connections;

public sealed record ConnectionRequestResponse(Guid Id, Guid SenderId, Guid RecipientId, ConnectionRequestStatus Status, DateTime CreatedAtUtc);
public sealed record ConnectionResponse(Guid UserId, Guid ConnectedUserId, DateTime ConnectedAtUtc);

public interface IConnectionService
{
    Task<ConnectionRequestResponse> SendAsync(Guid sender, Guid recipient, CancellationToken cancellationToken);
    Task<PagedResult<ConnectionRequestResponse>> ListRequestsAsync(Guid userId, bool incoming, PagedRequest page, CancellationToken cancellationToken);
    Task AcceptAsync(Guid userId, Guid requestId, CancellationToken cancellationToken);
    Task DeclineAsync(Guid userId, Guid requestId, CancellationToken cancellationToken);
    Task CancelRequestAsync(Guid userId, Guid requestId, CancellationToken cancellationToken);
    Task<PagedResult<ConnectionResponse>> ListConnectionsAsync(Guid userId, PagedRequest page, CancellationToken cancellationToken);
    Task RemoveAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken);
    Task BlockAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken);
    Task UnblockAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken);
    Task<PagedResult<Guid>> ListBlockedAsync(Guid userId, PagedRequest page, CancellationToken cancellationToken);
}

public sealed class ConnectionService(IApplicationDbContext db, IClock clock, UniConnect.Application.Notifications.INotificationService notifications) : IConnectionService
{
    public async Task<ConnectionRequestResponse> SendAsync(Guid sender, Guid recipient, CancellationToken cancellationToken)
    {
        if (sender == recipient) throw new ArgumentException("A user cannot connect with themselves.");
        if (!await db.Users.AnyAsync(x => x.Id == recipient && x.IsActive, cancellationToken)) throw new KeyNotFoundException("User was not found.");
        if (!await db.Profiles.AnyAsync(x => x.Id == recipient && x.AllowConnectionRequests, cancellationToken)) throw new AccessDeniedException("This user is not accepting connection requests.");
        if (await IsBlocked(sender, recipient, cancellationToken)) throw new AccessDeniedException("A blocked relationship prevents connection requests.");
        if (await AreConnected(sender, recipient, cancellationToken)) throw new InvalidOperationException("Users are already connected.");
        var previous = await db.ConnectionRequests.Where(x => (x.SenderId == sender && x.RecipientId == recipient) || (x.SenderId == recipient && x.RecipientId == sender)).OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (previous is { Status: ConnectionRequestStatus.Declined or ConnectionRequestStatus.Cancelled } && previous.SenderId == sender && previous.RecipientId == recipient)
        {
            previous.Reopen(clock.UtcNow); await db.SaveChangesAsync(cancellationToken);
            await notifications.CreateAsync(recipient, "ConnectionRequest", "New connection request", "You received a connection request.", "ConnectionRequest", previous.Id, cancellationToken);
            return Map(previous);
        }
        var pending = await db.ConnectionRequests.AnyAsync(x => x.Status == ConnectionRequestStatus.Pending &&
            ((x.SenderId == sender && x.RecipientId == recipient) || (x.SenderId == recipient && x.RecipientId == sender)), cancellationToken);
        if (pending) throw new InvalidOperationException("A pending connection request already exists.");
        var request = new ConnectionRequest(sender, recipient, clock.UtcNow);
        await db.ConnectionRequests.AddAsync(request, cancellationToken); await db.SaveChangesAsync(cancellationToken);
        await notifications.CreateAsync(recipient, "ConnectionRequest", "New connection request", "You received a connection request.", "ConnectionRequest", request.Id, cancellationToken);
        return Map(request);
    }

    public async Task<PagedResult<ConnectionRequestResponse>> ListRequestsAsync(Guid userId, bool incoming, PagedRequest page, CancellationToken cancellationToken)
    {
        var query = db.ConnectionRequests.AsNoTracking().Where(x => x.Status == ConnectionRequestStatus.Pending && (incoming ? x.RecipientId == userId : x.SenderId == userId));
        var total = await query.LongCountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).Skip(page.Skip).Take(page.SafePageSize)
            .Select(x => new ConnectionRequestResponse(x.Id, x.SenderId, x.RecipientId, x.Status, x.CreatedAtUtc)).ToListAsync(cancellationToken);
        return new(items, page.SafePageNumber, page.SafePageSize, total);
    }

    public async Task AcceptAsync(Guid userId, Guid requestId, CancellationToken cancellationToken)
    {
        var request = await db.ConnectionRequests.SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken) ?? throw new KeyNotFoundException("Request was not found.");
        if (request.RecipientId != userId) throw new AccessDeniedException("Only the recipient can accept this request.");
        if (await IsBlocked(request.SenderId, request.RecipientId, cancellationToken)) throw new AccessDeniedException("Blocked users cannot connect.");
        request.Accept(clock.UtcNow);
        await db.Connections.AddAsync(new Connection(request.SenderId, request.RecipientId, clock.UtcNow), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await notifications.CreateAsync(request.SenderId, "ConnectionAccepted", "Connection request accepted", "Your connection request was accepted.", "ConnectionRequest", request.Id, cancellationToken);
    }

    public async Task DeclineAsync(Guid userId, Guid requestId, CancellationToken cancellationToken)
    {
        var request = await db.ConnectionRequests.SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken) ?? throw new KeyNotFoundException("Request was not found.");
        if (request.RecipientId != userId) throw new AccessDeniedException("Only the recipient can decline this request.");
        request.Decline(clock.UtcNow); await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelRequestAsync(Guid userId, Guid requestId, CancellationToken cancellationToken)
    {
        var request = await db.ConnectionRequests.SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("Connection request was not found.");
        if (request.SenderId != userId) throw new AccessDeniedException("Only the sender can withdraw this request.");
        request.Cancel(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }
    public async Task<PagedResult<ConnectionResponse>> ListConnectionsAsync(Guid userId, PagedRequest page, CancellationToken cancellationToken)
    {
        var query = db.Connections.AsNoTracking().Where(x => x.UserId == userId || x.ConnectedUserId == userId);
        var total = await query.LongCountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).Skip(page.Skip).Take(page.SafePageSize)
            .Select(x => new ConnectionResponse(x.UserId, x.ConnectedUserId, x.CreatedAtUtc)).ToListAsync(cancellationToken);
        return new(items, page.SafePageNumber, page.SafePageSize, total);
    }

    public async Task RemoveAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken)
    {
        var connection = await db.Connections.SingleOrDefaultAsync(x => (x.UserId == userId && x.ConnectedUserId == otherUserId) || (x.UserId == otherUserId && x.ConnectedUserId == userId), cancellationToken);
        if (connection is not null) { db.Connections.Remove(connection); await db.SaveChangesAsync(cancellationToken); }
    }

    public async Task BlockAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken)
    {
        if (userId == otherUserId) throw new ArgumentException("A user cannot block themselves.");
        if (!await db.Users.AnyAsync(x => x.Id == otherUserId, cancellationToken)) throw new KeyNotFoundException("User was not found.");
        if (!await db.BlockedUsers.AnyAsync(x => x.BlockerId == userId && x.BlockedId == otherUserId, cancellationToken))
            await db.BlockedUsers.AddAsync(new BlockedUser(userId, otherUserId, clock.UtcNow), cancellationToken);
        var connection = await db.Connections.SingleOrDefaultAsync(x => (x.UserId == userId && x.ConnectedUserId == otherUserId) || (x.UserId == otherUserId && x.ConnectedUserId == userId), cancellationToken);
        if (connection is not null) db.Connections.Remove(connection);
        var requests = await db.ConnectionRequests.Where(x => x.Status == ConnectionRequestStatus.Pending && ((x.SenderId == userId && x.RecipientId == otherUserId) || (x.SenderId == otherUserId && x.RecipientId == userId))).ToListAsync(cancellationToken);
        foreach (var request in requests) request.Cancel(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UnblockAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken)
    {
        var block = await db.BlockedUsers.SingleOrDefaultAsync(x => x.BlockerId == userId && x.BlockedId == otherUserId, cancellationToken);
        if (block is not null) { db.BlockedUsers.Remove(block); await db.SaveChangesAsync(cancellationToken); }
    }

    public async Task<PagedResult<Guid>> ListBlockedAsync(Guid userId, PagedRequest page, CancellationToken cancellationToken)
    {
        var query = db.BlockedUsers.AsNoTracking().Where(x => x.BlockerId == userId).Select(x => x.BlockedId);
        var total = await query.LongCountAsync(cancellationToken);
        var items = await query.OrderBy(x => x).Skip(page.Skip).Take(page.SafePageSize).ToListAsync(cancellationToken);
        return new(items, page.SafePageNumber, page.SafePageSize, total);
    }

    private Task<bool> IsBlocked(Guid first, Guid second, CancellationToken ct) => db.BlockedUsers.AnyAsync(x => (x.BlockerId == first && x.BlockedId == second) || (x.BlockerId == second && x.BlockedId == first), ct);
    private Task<bool> AreConnected(Guid first, Guid second, CancellationToken ct) => db.Connections.AnyAsync(x => (x.UserId == first && x.ConnectedUserId == second) || (x.UserId == second && x.ConnectedUserId == first), ct);
    private static ConnectionRequestResponse Map(ConnectionRequest request) => new(request.Id, request.SenderId, request.RecipientId, request.Status, request.CreatedAtUtc);
}
