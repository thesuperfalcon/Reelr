using System.ComponentModel.DataAnnotations;

namespace backend.Features.Users.DTOs
{
    public class UpdateUserDto
    {
        [MinLength(User.UserNameMinLength)]
        [MaxLength(User.UserNameMaxLength)]
        // The profile picture is uploaded through PUT /api/settings/avatar instead.
        public string? Username { get; set; }
    }
}
