using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace backend.Features.MovieLists.DTOs
{
    public class UpdateMovieListDto
    {
        [DefaultValue(null)]
        [MinLength(1), MaxLength(100)]
        public string? Name { get; set; }

        [DefaultValue(null)]
        [MaxLength(1000)]
        public string? Description { get; set; }

        [DefaultValue(null)]
        public bool? IsPublic { get; set; }
    }
}
