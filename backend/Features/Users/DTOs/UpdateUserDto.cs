using System.ComponentModel.DataAnnotations;

namespace backend.Features.Users.DTOs
{
    public class UpdateUserDto
    {
        [MinLength(3)]
        public string? Username { get; set; }

        public string? ProfileImageUrl { get; set; }
    }
}
