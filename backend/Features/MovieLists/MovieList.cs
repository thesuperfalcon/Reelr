namespace backend.Features.MovieLists
{
    public class MovieList
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public Users.User User { get; set; } = null!;

        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public bool IsPublic { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public ICollection<MovieListItem> Items { get; set; } = [];
    }
}
