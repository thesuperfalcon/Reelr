namespace backend.Features.Auth;

/// <summary>
/// A long-lived sign-in kept in an HttpOnly cookie, exchanged for short access tokens (see RefreshTokens).
/// Only a hash of the token is stored. Each use replaces the token with a new one in the same family;
/// a used token that comes back means it was copied, and the whole family is revoked.
/// </summary>
public class RefreshToken
{
    // SHA-256 as 64 hex characters.
    public const int HashLength = 64;

    public long Id { get; set; }

    public int UserId { get; set; }
    public Users.User User { get; set; } = null!;

    public string TokenHash { get; set; } = null!;

    // Every token that came from one login shares the family id.
    public Guid FamilyId { get; set; }

    // The user's security stamp when the token was made. A password change, rename or "log out everywhere"
    // changes the stamp, which ends every refresh token made before it.
    public string SecurityStamp { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    // Set when the token was exchanged for a new one.
    public DateTime? UsedAt { get; set; }

    // Set on logout, or for the whole family when a used token comes back.
    public DateTime? RevokedAt { get; set; }
}
