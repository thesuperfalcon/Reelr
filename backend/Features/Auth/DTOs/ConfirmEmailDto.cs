using System.ComponentModel.DataAnnotations;

namespace backend.Features.Auth.DTOs;

// The user id and token from the link in the confirmation email.
public class ConfirmEmailDto
{
    [Required]
    public int? UserId { get; set; }

    [Required]
    [MaxLength(2048)]
    public string Token { get; set; } = string.Empty;
}
