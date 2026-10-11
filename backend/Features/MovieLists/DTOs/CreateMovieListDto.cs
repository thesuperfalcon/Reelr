using System.ComponentModel.DataAnnotations;

namespace backend.Features.MovieLists.DTOs
{
    public class CreateMovieListDto
    {
        [Required, MinLength(1), MaxLength(MovieList.NameMaxLength)]
        public string Name { get; set; } = null!;
        [MaxLength(MovieList.DescriptionMaxLength)]
        public string? Description { get; set; }
        public bool IsPublic { get; set; } = true;
    }
}
