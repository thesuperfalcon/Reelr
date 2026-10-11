using System.ComponentModel.DataAnnotations;

namespace backend.Features.Auth.DTOs;

// The user id and token from the link in the reset email, and the new password.
public class ResetPasswordDto
{
    [Required]
    public int? UserId { get; set; }

    [Required]
    [MaxLength(2048)]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
}
