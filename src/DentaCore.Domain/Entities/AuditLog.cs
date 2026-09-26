using DentaCore.Domain.Common;

namespace DentaCore.Domain.Entities;

/// <summary>
/// Immutable audit log entry — automatically populated via EF Core SaveChanges interceptor.
/// Records every meaningful state change in the system.
/// </summary>
public class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;       // e.g. "Updated", "Created", "Deleted"
    public string EntityName { get; set; } = string.Empty;    // e.g. "Appointment"
    public Guid? EntityId { get; set; }
    public string? OldValue { get; set; }                     // JSON snapshot of previous state
    public string? NewValue { get; set; }                     // JSON snapshot of new state
    public string? IpAddress { get; set; }

    // Navigation
    public User? User { get; set; }
}
