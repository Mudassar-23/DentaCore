using DentaCore.Domain.Common;

namespace DentaCore.Domain.Entities;

/// <summary>
/// In-app notification pushed to users on appointment and payment state changes.
/// </summary>
public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
