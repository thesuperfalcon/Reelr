namespace backend.Features.Movies
{
    public class Movie
    {
        public int Id { get; set; }

        public int TmdbId { get; set; }

        public string Title { get; set; } = null!;

        public string? Description { get; set; }

        public DateOnly? ReleaseDate { get; set; }

        public int? Runtime { get; set; }

        public string? PosterUrl { get; set; }

        public string? BackdropUrl { get; set; }

        public ICollection<WatchedMovies.WatchedMovie> WatchedMovies { get; set; } = [];

        public ICollection<Ratings.Rating> Ratings { get; set; } = [];

        public ICollection<Reviews.Review> Reviews { get; set; } = [];

        public ICollection<WatchlistItems.WatchlistItem> WatchlistItems { get; set; } = [];
    }
}
