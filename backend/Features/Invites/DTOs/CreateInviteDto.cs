using System.ComponentModel.DataAnnotations;

namespace backend.Features.Invites.DTOs;

public class CreateInviteDto
{
    [MaxLength(Invite.NoteMaxLength)]
    public string? Note { get; set; }
}
