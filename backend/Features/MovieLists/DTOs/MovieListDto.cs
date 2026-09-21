namespace backend.Features.MovieLists.DTOs
{
    public class MovieListDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsPublic { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int MovieCount { get; set; }
        public List<MovieListItemDto> TopMovies { get; set; } = [];
    }
}
