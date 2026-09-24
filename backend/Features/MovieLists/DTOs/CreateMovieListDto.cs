using System.ComponentModel.DataAnnotations;

namespace backend.Features.MovieLists.DTOs
{
    public class CreateMovieListDto
    {
        [Required, MinLength(1), MaxLength(100)]
        public string Name { get; set; } = null!;
        [MaxLength(1000)]
        public string? Description { get; set; }
        public bool IsPublic { get; set; } = true;
    }
}
