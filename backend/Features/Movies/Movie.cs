namespace backend.Features.Movies
{
    public class Movie
    {
        // Column sizes. Film data comes from TMDB, so MovieCatalog cuts longer values to fit.
        public const int TitleMaxLength = 500;
        public const int DescriptionMaxLength = 4000;
        public const int ImagePathMaxLength = 300;

        public int Id { get; set; }

        public int TmdbId { get; set; }

        public string Title { get; set; } = null!;

        public string? Description { get; set; }

        public DateOnly? ReleaseDate { get; set; }

        public int? Runtime { get; set; }

        public string? PosterUrl { get; set; }

        // When the data above was last copied from TMDB. MovieRefresher brings old copies up to date.
        public DateTime RefreshedAt { get; set; }

        public string? BackdropUrl { get; set; }

        public ICollection<WatchedMovies.WatchedMovie> WatchedMovies { get; set; } = [];

        public ICollection<Ratings.Rating> Ratings { get; set; } = [];

        public ICollection<Reviews.Review> Reviews { get; set; } = [];

        public ICollection<WatchlistItems.WatchlistItem> WatchlistItems { get; set; } = [];
    }
}
