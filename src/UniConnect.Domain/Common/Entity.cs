namespace UniConnect.Domain.Common;

public abstract class Entity<TId> where TId : notnull
{
    protected Entity(TId id) => Id = id;
    public TId Id { get; protected set; }
}

public interface IAuditableEntity
{
    DateTime CreatedAtUtc { get; }
    DateTime UpdatedAtUtc { get; }
}
