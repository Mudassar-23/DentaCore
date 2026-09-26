using DentaCore.Domain.Common;
using DentaCore.Domain.Enums;

namespace DentaCore.Domain.Entities;

/// <summary>
/// Identity table — single source for authentication. 
/// Role-specific profiles (Patient, Doctor, Admin) are 1:1 extension tables.
/// </summary>
public class User : BaseEntity, IAuditable, ISoftDeletable
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public Patient? Patient { get; set; }
    public Doctor? Doctor { get; set; }
    public Admin? Admin { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
