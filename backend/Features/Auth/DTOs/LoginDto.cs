using System.ComponentModel.DataAnnotations;

namespace backend.Features.Auth.DTOs
{
    public class LoginDto
    {
        [Required]
        [MinLength(3)]
        public string UserInput { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        public string Password { get; set; } = string.Empty;
    }
}
