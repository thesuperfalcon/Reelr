namespace backend.Features.MovieLists.DTOs
{
    public class MovieListSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public bool IsPublic { get; set; }
        public int MovieCount { get; set; }
        public List<MovieListItemDto> TopMovies { get; set; } = [];
        public DateTime CreatedAt { get; set; }
    }
}
