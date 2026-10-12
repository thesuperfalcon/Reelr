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

    // The sign-up form's checkbox: the user is at least 13 and accepts the terms and the privacy policy.
    [Range(typeof(bool), "true", "true", ErrorMessage = "Accept the terms and privacy policy to create an account.")]
    public bool AcceptTerms { get; set; }
}
