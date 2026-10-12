namespace backend.Features.Invites.DTOs;

// Returned once, when the invite is made: the code is not stored, so it cannot be shown again.
public class CreatedInviteDto : InviteDto
{
    public string Code { get; set; } = string.Empty;

    // The sign-up page with the code filled in, ready to send.
    public string Link { get; set; } = string.Empty;
}
