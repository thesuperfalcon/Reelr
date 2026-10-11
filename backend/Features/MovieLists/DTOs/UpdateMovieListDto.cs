using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace backend.Features.MovieLists.DTOs
{
    public class UpdateMovieListDto
    {
        [DefaultValue(null)]
        [MinLength(1), MaxLength(MovieList.NameMaxLength)]
        public string? Name { get; set; }

        [DefaultValue(null)]
        [MaxLength(MovieList.DescriptionMaxLength)]
        public string? Description { get; set; }

        [DefaultValue(null)]
        public bool? IsPublic { get; set; }
    }
}
