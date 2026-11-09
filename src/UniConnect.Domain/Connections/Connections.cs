using UniConnect.Domain.Common;

namespace UniConnect.Domain.Connections;

public enum ConnectionRequestStatus { Pending, Accepted, Declined, Cancelled }

public sealed class ConnectionRequest : Entity<Guid>, IAuditableEntity
{
    private ConnectionRequest() : base(Guid.Empty) { }
    public ConnectionRequest(Guid senderId, Guid recipientId, DateTime nowUtc) : base(Guid.NewGuid())
    {
        if (senderId == recipientId) throw new InvalidOperationException("A user cannot connect with themselves.");
        SenderId = senderId; RecipientId = recipientId; Status = ConnectionRequestStatus.Pending; CreatedAtUtc = UpdatedAtUtc = nowUtc;
    }
    public Guid SenderId { get; private set; }
    public Guid RecipientId { get; private set; }
    public ConnectionRequestStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public void Accept(DateTime nowUtc) => Complete(ConnectionRequestStatus.Accepted, nowUtc);
    public void Decline(DateTime nowUtc) => Complete(ConnectionRequestStatus.Declined, nowUtc);
    public void Cancel(DateTime nowUtc) => Complete(ConnectionRequestStatus.Cancelled, nowUtc);
    public void Reopen(DateTime nowUtc) { if (Status is not (ConnectionRequestStatus.Declined or ConnectionRequestStatus.Cancelled)) throw new InvalidOperationException("Only a declined or cancelled request can be reopened."); Status = ConnectionRequestStatus.Pending; UpdatedAtUtc = nowUtc; }
    private void Complete(ConnectionRequestStatus status, DateTime nowUtc)
    {
        if (Status != ConnectionRequestStatus.Pending) throw new InvalidOperationException("Only pending connection requests can be completed.");
        Status = status; UpdatedAtUtc = nowUtc;
    }
}

public sealed class Connection
{
    private Connection() { }
    public Connection(Guid firstUserId, Guid secondUserId, DateTime createdAtUtc)
    {
        if (firstUserId == secondUserId) throw new InvalidOperationException("A user cannot connect with themselves.");
        (UserId, ConnectedUserId) = firstUserId.CompareTo(secondUserId) < 0 ? (firstUserId, secondUserId) : (secondUserId, firstUserId);
        CreatedAtUtc = createdAtUtc;
    }
    public Guid UserId { get; private set; }
    public Guid ConnectedUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}

public sealed class BlockedUser
{
    private BlockedUser() { }
    public BlockedUser(Guid blockerId, Guid blockedId, DateTime createdAtUtc)
    {
        if (blockerId == blockedId) throw new InvalidOperationException("A user cannot block themselves.");
        BlockerId = blockerId; BlockedId = blockedId; CreatedAtUtc = createdAtUtc;
    }
    public Guid BlockerId { get; private set; }
    public Guid BlockedId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
