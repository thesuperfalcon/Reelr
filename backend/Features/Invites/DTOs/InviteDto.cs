namespace backend.Features.Invites.DTOs;

public class InviteDto
{
    public int Id { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UsedAt { get; set; }

    // The account that used the invite, if it still exists.
    public string? UsedBy { get; set; }

    public DateTime? RevokedAt { get; set; }
}
