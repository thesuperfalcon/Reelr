namespace backend.Features.Diary.DTOs
{
    public class DiaryEntryDto
    {
        public int Id { get; set; }

        public int TmdbId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? PosterUrl { get; set; }

        public decimal? Rating { get; set; }

        public bool? Liked { get; set; }

        public bool Rewatched { get; set; }

        // True when this entry logged the user's review of the film.
        public bool HasReview { get; set; }

        public DateTime WatchedAt { get; set; }
    }
}
