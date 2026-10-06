namespace backend.Features.Diary
{
    // One logged viewing. Rating, liked and rewatched are copied from the moment it was logged,
    // so changing a rating later adds a new entry instead of rewriting old ones.
    public class DiaryEntry
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public Users.User User { get; set; } = null!;

        public int MovieId { get; set; }

        public Movies.Movie Movie { get; set; } = null!;

        public DateTime WatchedAt { get; set; }

        public decimal? Rating { get; set; }

        public bool? Liked { get; set; }

        public bool Rewatched { get; set; }
    }
}
