namespace DentaCore.Domain.Common;

/// <summary>
/// Base entity with common fields for all domain entities.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Interface for entities that track modification timestamps.
/// </summary>
public interface IAuditable
{
    DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Interface for entities that support soft deletion.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
}
