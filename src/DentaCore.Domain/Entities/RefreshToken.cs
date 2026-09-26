using DentaCore.Domain.Common;

namespace DentaCore.Domain.Entities;

/// <summary>
/// Refresh token for JWT rotation — stored hashed, revocable on logout/password change.
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
