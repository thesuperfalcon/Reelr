using System.ComponentModel.DataAnnotations;

namespace backend.Features.Auth.DTOs;

// For "send the confirmation email again" and "forgot password".
public class EmailDto
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;
}
