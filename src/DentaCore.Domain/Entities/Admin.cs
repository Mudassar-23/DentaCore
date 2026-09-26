using DentaCore.Domain.Common;

namespace DentaCore.Domain.Entities;

/// <summary>
/// 1:1 profile extension for admins.
/// </summary>
public class Admin : BaseEntity
{
    public Guid UserId { get; set; }
    public string? Designation { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
