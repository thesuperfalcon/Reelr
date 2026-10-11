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

        // The day the film was watched. Can be back-dated and edited.
        public DateTime WatchedAt { get; set; }

        // When the entry was saved. Never changes; the activity feed orders by it.
        public DateTime LoggedAt { get; set; }

        // A log saved more than this many days after the watch date counts as back-dated
        // and stays out of the community feed, so back-filling an old diary does not flood it.
        public const int BackdatedDays = 14;

        // Computed by the database from LoggedAt and WatchedAt, so the feed can filter on it through an index.
        public bool IsBackdated { get; private set; }

        public decimal? Rating { get; set; }

        public bool? Liked { get; set; }

        public bool Rewatched { get; set; }
    }
}
