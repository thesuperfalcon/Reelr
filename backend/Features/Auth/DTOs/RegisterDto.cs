using System.ComponentModel.DataAnnotations;
using backend.Features.Users;

namespace backend.Features.Auth.DTOs;

public class RegisterDto
{
    [Required]
    [MinLength(User.UserNameMinLength)]
    [MaxLength(User.UserNameMaxLength)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;
}
