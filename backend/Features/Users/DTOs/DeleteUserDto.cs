using System.ComponentModel.DataAnnotations;

namespace backend.Features.Users.DTOs
{
    public class DeleteUserDto
    {
        // The current password confirms the deletion, so a stolen token alone cannot delete the account.
        [Required]
        public string Password { get; set; } = string.Empty;
    }
}
