using backend.Data;
using backend.Features.WatchedMovies;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.Diary
{
    public static class DiaryExtensions
    {
        // The first public film screening was in 1888.
        private static readonly DateOnly EarliestWatchDate = new(1888, 1, 1);

        // Adds a diary entry that snapshots the current status and rating. Saved with the caller's SaveChangesAsync.
        // A null watchedAt means now.
        public static DiaryEntry LogDiaryEntry(this ReelrContext context, WatchedMovie status, decimal? rating, DateTime? watchedAt = null)
        {
            var now = DateTime.UtcNow;
            var entry = new DiaryEntry
            {
                UserId = status.UserId,
                MovieId = status.MovieId,
                WatchedAt = watchedAt ?? now,
                LoggedAt = now,
                Rating = rating,
                Liked = status.Liked,
                Rewatched = status.Rewatched
            };

            context.DiaryEntries.Add(entry);

            return entry;
        }

        // A picked day is stored at noon UTC, so it shows as the same calendar day in every time zone from UTC-12 to UTC+12.
        public static DateTime ToWatchedAt(DateOnly watchedOn) =>
            watchedOn.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);

        // Returns an error message, or null when the day is allowed.
        // Today in the furthest time zone (UTC+14) can be tomorrow in UTC, so one day ahead is allowed.
        public static string? ValidateWatchedOn(DateOnly watchedOn)
        {
            if (watchedOn > DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
            {
                return "Watch date cannot be in the future.";
            }

            if (watchedOn < EarliestWatchDate)
            {
                return "Watch date cannot be before 1888.";
            }

            return null;
        }

        // Deletes diary entries together with the reviews they logged. Saved with the caller's SaveChangesAsync.
        public static async Task DeleteDiaryEntriesAsync(this ReelrContext context, IReadOnlyCollection<DiaryEntry> entries)
        {
            var ids = entries.Select(e => e.Id).ToList();

            context.Reviews.RemoveRange(await context.Reviews
                .Where(r => r.DiaryEntryId != null && ids.Contains(r.DiaryEntryId.Value))
                .ToListAsync());

            context.DiaryEntries.RemoveRange(entries);
        }

        // Keeps the watched status in step with the saved diary: it takes the latest entry's date,
        // and goes away when no entries are left. The rating is left alone.
        public static async Task SyncWatchedStatusAsync(this ReelrContext context, int userId, int movieId)
        {
            var status = await context.WatchedMovies
                .FirstOrDefaultAsync(w => w.UserId == userId && w.MovieId == movieId);

            if (status == null)
            {
                return;
            }

            var latest = await context.DiaryEntries
                .Where(d => d.UserId == userId && d.MovieId == movieId)
                .MaxAsync(d => (DateTime?)d.WatchedAt);

            if (latest == null)
            {
                context.WatchedMovies.Remove(status);
            }
            else
            {
                status.WatchedAt = latest.Value;
            }
        }
    }
}
