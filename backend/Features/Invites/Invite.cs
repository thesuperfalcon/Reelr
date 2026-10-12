namespace backend.Features.Invites;

/// <summary>
/// A one-time code that lets one person create an account while registration is invite only (see InviteCodes).
/// Only a hash of the code is stored; the admin sees the code once, when it is made.
/// </summary>
public class Invite
{
    // SHA-256 as 64 hex characters.
    public const int HashLength = 64;
    public const int NoteMaxLength = 100;

    public int Id { get; set; }

    public string CodeHash { get; set; } = null!;

    // Who the invite is for, so the admin can tell the invites apart.
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    // Set when someone registered with the code. The account may since have been deleted.
    public DateTime? UsedAt { get; set; }
    public int? UsedById { get; set; }
    public Users.User? UsedBy { get; set; }

    // Set when the admin took the invite back before it was used.
    public DateTime? RevokedAt { get; set; }
}
