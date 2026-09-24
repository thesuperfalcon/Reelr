using System.ComponentModel.DataAnnotations;

namespace backend.Features.MovieLists.DTOs
{
    public class AddMovieToListDto
    {
        [Required]
        public int TmdbId { get; set; }
    }
}
