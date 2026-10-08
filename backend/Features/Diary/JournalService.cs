using backend.Data;
using backend.Features.Ratings;
using backend.Features.Reviews;
using backend.Features.WatchedMovies;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.Diary
{
    // The rules that tie a user's diary, watched status, rating, review and watchlist together.
    // Every change that logs or removes a viewing goes through here, whichever endpoint it comes from:
    // - each log adds a diary entry that snapshots the status and rating at that moment,
    // - a logged film counts as watched and leaves the watchlist,
    // - the watched status follows the newest diary entry and goes away with the last one,
    // - a review written while logging belongs to that entry and is deleted with it.
    // Each method saves its own changes.
    public class JournalService
    {
        // The first public film screening was in 1888.
        private static readonly DateOnly EarliestWatchDate = new(1888, 1, 1);

        private readonly ReelrContext _context;

        public JournalService(ReelrContext context)
        {
            _context = context;
        }

        // Scores go from 0 to 5 in half steps.
        public static bool IsHalfStep(decimal score) => decimal.Remainder(score * 2, 1) == 0;

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

        // Saves rating, status and review in one go and logs them as one diary entry.
        // A null score leaves the rating as it is; a blank review leaves the review as it is.
        public async Task<(DiaryEntry Entry, bool ReviewWritten)> LogAsync(
            int userId, int movieId, DateTime watchedAt, decimal? score, bool? liked, bool rewatched,
            string? reviewText, bool? containsSpoilers)
        {
            var rating = await _context.Ratings
                .FirstOrDefaultAsync(r => r.UserId == userId && r.MovieId == movieId);

            if (score != null)
            {
                if (rating == null)
                {
                    rating = new Rating { UserId = userId, MovieId = movieId };
                    _context.Ratings.Add(rating);
                }

                rating.Score = score.Value;
            }

            var status = await _context.WatchedMovies
                .FirstOrDefaultAsync(w => w.UserId == userId && w.MovieId == movieId);

            if (status == null)
            {
                status = new WatchedMovie
                {
                    UserId = userId,
                    MovieId = movieId,
                    WatchedAt = watchedAt
                };

                _context.WatchedMovies.Add(status);
            }
            else if (watchedAt > status.WatchedAt)
            {
                // A back-dated log does not move the last watch date backwards.
                status.WatchedAt = watchedAt;
            }

            status.Liked = liked;
            status.Rewatched = rewatched;

            var entry = AddEntry(status, rating?.Score, watchedAt);
            var reviewWritten = !string.IsNullOrWhiteSpace(reviewText);

            if (reviewWritten)
            {
                var review = await _context.Reviews
                    .FirstOrDefaultAsync(r => r.UserId == userId && r.MovieId == movieId);

                if (review == null)
                {
                    review = new Review
                    {
                        UserId = userId,
                        MovieId = movieId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Reviews.Add(review);
                }
                else
                {
                    review.UpdatedAt = DateTime.UtcNow;
                }

                review.Text = reviewText!.Trim();
                review.ContainsSpoilers = containsSpoilers ?? review.ContainsSpoilers;

                // A written or rewritten review belongs to the entry that logged it.
                review.DiaryEntry = entry;
            }

            await RemoveFromWatchlistAsync(userId, movieId);
            await _context.SaveChangesAsync();

            return (entry, reviewWritten);
        }

        // Marks the film watched now, or updates the existing status, and logs it with the current rating.
        // An existing status keeps its watch date.
        public async Task<WatchedMovie> MarkWatchedAsync(int userId, int movieId, bool? liked, bool rewatched)
        {
            var status = await _context.WatchedMovies
                .FirstOrDefaultAsync(w => w.UserId == userId && w.MovieId == movieId);

            if (status == null)
            {
                status = new WatchedMovie
                {
                    UserId = userId,
                    MovieId = movieId,
                    WatchedAt = DateTime.UtcNow
                };

                _context.WatchedMovies.Add(status);
            }

            status.Liked = liked;
            status.Rewatched = rewatched;

            var rating = await _context.Ratings
                .Where(r => r.UserId == userId && r.MovieId == movieId)
                .Select(r => (decimal?)r.Score)
                .FirstOrDefaultAsync();

            AddEntry(status, rating);
            await RemoveFromWatchlistAsync(userId, movieId);
            await _context.SaveChangesAsync();

            return status;
        }

        // Sets the rating, creating it when needed. A rated film counts as watched, so each rating logs a new diary entry.
        public async Task<Rating> RateAsync(int userId, int movieId, decimal score)
        {
            var rating = await _context.Ratings
                .FirstOrDefaultAsync(r => r.UserId == userId && r.MovieId == movieId);

            if (rating == null)
            {
                rating = new Rating { UserId = userId, MovieId = movieId };
                _context.Ratings.Add(rating);
            }

            rating.Score = score;

            await RemoveFromWatchlistAsync(userId, movieId);

            var status = await _context.WatchedMovies
                .FirstOrDefaultAsync(w => w.UserId == userId && w.MovieId == movieId);

            if (status == null)
            {
                status = new WatchedMovie
                {
                    UserId = userId,
                    MovieId = movieId,
                    WatchedAt = DateTime.UtcNow
                };

                _context.WatchedMovies.Add(status);
            }

            AddEntry(status, score);
            await _context.SaveChangesAsync();

            return rating;
        }

        // An unwatched film has no viewings, so its diary entries and the reviews they logged go too.
        public async Task UnwatchAsync(WatchedMovie status)
        {
            await DeleteEntriesAsync(await _context.DiaryEntries
                .Where(d => d.UserId == status.UserId && d.MovieId == status.MovieId)
                .ToListAsync());

            _context.WatchedMovies.Remove(status);
            await _context.SaveChangesAsync();
        }

        // Replaces what one entry records. The film's current rating stays as it is.
        public async Task EditEntryAsync(DiaryEntry entry, DateOnly watchedOn, decimal? rating, bool? liked, bool rewatched)
        {
            // Keep the original time when the day did not change, so same-day entries keep their order.
            if (DateOnly.FromDateTime(entry.WatchedAt) != watchedOn)
            {
                entry.WatchedAt = ToWatchedAt(watchedOn);
            }

            entry.Rating = rating;
            entry.Liked = liked;
            entry.Rewatched = rewatched;

            await _context.SaveChangesAsync();
            await SyncWatchedStatusAsync(entry.UserId, entry.MovieId);
            await _context.SaveChangesAsync();
        }

        // Deletes one entry and its review. Deleting the last entry marks the film unwatched.
        public async Task DeleteEntryAsync(DiaryEntry entry)
        {
            await DeleteEntriesAsync([entry]);
            await _context.SaveChangesAsync();
            await SyncWatchedStatusAsync(entry.UserId, entry.MovieId);
            await _context.SaveChangesAsync();
        }

        // Adds a diary entry that snapshots the current status and rating. A null watchedAt means now.
        private DiaryEntry AddEntry(WatchedMovie status, decimal? rating, DateTime? watchedAt = null)
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

            _context.DiaryEntries.Add(entry);

            return entry;
        }

        private async Task DeleteEntriesAsync(IReadOnlyCollection<DiaryEntry> entries)
        {
            var ids = entries.Select(e => e.Id).ToList();

            _context.Reviews.RemoveRange(await _context.Reviews
                .Where(r => r.DiaryEntryId != null && ids.Contains(r.DiaryEntryId.Value))
                .ToListAsync());

            _context.DiaryEntries.RemoveRange(entries);
        }

        // Keeps the watched status in step with the saved diary: it takes the latest entry's date,
        // and goes away when no entries are left. The rating is left alone.
        private async Task SyncWatchedStatusAsync(int userId, int movieId)
        {
            var status = await _context.WatchedMovies
                .FirstOrDefaultAsync(w => w.UserId == userId && w.MovieId == movieId);

            if (status == null)
            {
                return;
            }

            var latest = await _context.DiaryEntries
                .Where(d => d.UserId == userId && d.MovieId == movieId)
                .MaxAsync(d => (DateTime?)d.WatchedAt);

            if (latest == null)
            {
                _context.WatchedMovies.Remove(status);
            }
            else
            {
                status.WatchedAt = latest.Value;
            }
        }

        // A watched film leaves the watchlist. The user can add it again afterwards; the next log removes it again.
        private async Task RemoveFromWatchlistAsync(int userId, int movieId)
        {
            var item = await _context.WatchlistItems
                .FirstOrDefaultAsync(w => w.UserId == userId && w.MovieId == movieId);

            if (item != null)
            {
                _context.WatchlistItems.Remove(item);
            }
        }
    }
}
