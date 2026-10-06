using backend.Data;
using backend.Features.WatchedMovies;

namespace backend.Features.Diary
{
    public static class DiaryExtensions
    {
        // Adds a diary entry that snapshots the current status and rating. Saved with the caller's SaveChangesAsync.
        public static DiaryEntry LogDiaryEntry(this ReelrContext context, WatchedMovie status, decimal? rating)
        {
            var entry = new DiaryEntry
            {
                UserId = status.UserId,
                MovieId = status.MovieId,
                WatchedAt = DateTime.UtcNow,
                Rating = rating,
                Liked = status.Liked,
                Rewatched = status.Rewatched
            };

            context.DiaryEntries.Add(entry);

            return entry;
        }
    }
}
