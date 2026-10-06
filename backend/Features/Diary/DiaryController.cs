using backend.Data;
using backend.Features.Diary.DTOs;
using backend.Features.Movies;
using backend.Features.Ratings;
using backend.Features.Reviews;
using backend.Features.WatchedMovies;
using backend.Features.WatchlistItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace backend.Features.Diary
{
    [Authorize]
    [ApiController]
    public class DiaryController : ControllerBase
    {
        private readonly ReelrContext _context;
        private readonly TmdbService _tmdbService;

        public DiaryController(ReelrContext context, TmdbService tmdbService)
        {
            _context = context;
            _tmdbService = tmdbService;
        }

        [HttpGet("api/watched")]
        [EndpointSummary("Get the current user's diary, newest entry first")]
        public async Task<ActionResult<List<DiaryEntryDto>>> GetDiary()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            return Ok(await GetEntries(userId));
        }

        [AllowAnonymous]
        [HttpGet("api/users/{userId:int}/diary")]
        [EndpointSummary("Get a user's diary, newest entry first")]
        public async Task<ActionResult<List<DiaryEntryDto>>> GetUserDiary(int userId)
        {
            if (!await _context.Users.AnyAsync(u => u.Id == userId))
            {
                return NotFound();
            }

            return Ok(await GetEntries(userId));
        }

        private Task<List<DiaryEntryDto>> GetEntries(int userId) =>
            _context.DiaryEntries
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.WatchedAt)
                .ThenByDescending(d => d.Id)
                .Select(d => new DiaryEntryDto
                {
                    Id = d.Id,
                    TmdbId = d.Movie.TmdbId,
                    Title = d.Movie.Title,
                    PosterUrl = d.Movie.PosterUrl,
                    Rating = d.Rating,
                    Liked = d.Liked,
                    Rewatched = d.Rewatched,
                    HasReview = _context.Reviews.Any(r => r.DiaryEntryId == d.Id),
                    WatchedAt = d.WatchedAt
                })
                .ToListAsync();

        [HttpPost("api/movies/{tmdbId:int}/diary")]
        [EndpointSummary("Save rating, status and review for a movie and log it as one diary entry")]
        public async Task<ActionResult<DiaryEntryDto>> LogEntry(int tmdbId, LogDiaryEntryDto dto)
        {
            if (dto.Score is decimal score && decimal.Remainder(score * 2, 1) != 0)
            {
                return BadRequest("Score must be in increments of 0.5.");
            }

            if (dto.WatchedOn is DateOnly watchedOn && DiaryExtensions.ValidateWatchedOn(watchedOn) is string dateError)
            {
                return BadRequest(dateError);
            }

            var watchedAt = dto.WatchedOn is DateOnly day ? DiaryExtensions.ToWatchedAt(day) : DateTime.UtcNow;
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var movie = await _context.Movies.FirstOrDefaultAsync(m => m.TmdbId == tmdbId);

            if (movie == null)
            {
                var tmdbMovie = await _tmdbService.GetMovie(tmdbId);

                if (tmdbMovie == null)
                {
                    return NotFound();
                }

                movie = new Movie
                {
                    TmdbId = tmdbId,
                    Title = tmdbMovie.Title ?? string.Empty,
                    Description = tmdbMovie.Overview,
                    ReleaseDate = DateOnly.TryParse(tmdbMovie.ReleaseDate, out var releaseDate) ? releaseDate : null,
                    Runtime = tmdbMovie.Runtime,
                    PosterUrl = tmdbMovie.PosterPath,
                    BackdropUrl = tmdbMovie.BackdropPath
                };

                _context.Movies.Add(movie);
                await _context.SaveChangesAsync();
            }

            var rating = await _context.Ratings
                .FirstOrDefaultAsync(r => r.UserId == userId && r.MovieId == movie.Id);

            if (dto.Score != null)
            {
                if (rating == null)
                {
                    rating = new Rating { UserId = userId, MovieId = movie.Id };
                    _context.Ratings.Add(rating);
                }

                rating.Score = dto.Score.Value;
            }

            var status = await _context.WatchedMovies
                .FirstOrDefaultAsync(w => w.UserId == userId && w.MovieId == movie.Id);

            if (status == null)
            {
                status = new WatchedMovie
                {
                    UserId = userId,
                    MovieId = movie.Id,
                    WatchedAt = watchedAt
                };

                _context.WatchedMovies.Add(status);
            }
            else if (watchedAt > status.WatchedAt)
            {
                // A back-dated log does not move the last watch date backwards.
                status.WatchedAt = watchedAt;
            }

            status.Liked = dto.Liked;
            status.Rewatched = dto.Rewatched;

            var review = await _context.Reviews
                .FirstOrDefaultAsync(r => r.UserId == userId && r.MovieId == movie.Id);

            if (!string.IsNullOrWhiteSpace(dto.Review))
            {
                if (review == null)
                {
                    review = new Review
                    {
                        UserId = userId,
                        MovieId = movie.Id,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Reviews.Add(review);
                }
                else
                {
                    review.UpdatedAt = DateTime.UtcNow;
                }

                review.Text = dto.Review.Trim();
            }

            var entry = _context.LogDiaryEntry(status, rating?.Score, watchedAt);
            var reviewWritten = !string.IsNullOrWhiteSpace(dto.Review);

            // A written or rewritten review belongs to the entry that logged it.
            if (reviewWritten)
            {
                review!.DiaryEntry = entry;
            }

            await _context.RemoveWatchedFromWatchlistAsync(userId, movie.Id);
            await _context.SaveChangesAsync();

            return Ok(new DiaryEntryDto
            {
                Id = entry.Id,
                TmdbId = tmdbId,
                Title = movie.Title,
                PosterUrl = movie.PosterUrl,
                Rating = entry.Rating,
                Liked = entry.Liked,
                Rewatched = entry.Rewatched,
                HasReview = reviewWritten,
                WatchedAt = entry.WatchedAt
            });
        }

        [HttpPut("api/diary/{entryId:int}")]
        [EndpointSummary("Edit one of the current user's diary entries")]
        public async Task<ActionResult<DiaryEntryDto>> UpdateEntry(int entryId, UpdateDiaryEntryDto dto)
        {
            if (dto.Rating is decimal score && decimal.Remainder(score * 2, 1) != 0)
            {
                return BadRequest("Rating must be in increments of 0.5.");
            }

            var watchedOn = dto.WatchedOn!.Value;

            if (DiaryExtensions.ValidateWatchedOn(watchedOn) is string dateError)
            {
                return BadRequest(dateError);
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var entry = await _context.DiaryEntries
                .Include(d => d.Movie)
                .FirstOrDefaultAsync(d => d.Id == entryId && d.UserId == userId);

            if (entry == null)
            {
                return NotFound();
            }

            // Keep the original time when the day did not change, so same-day entries keep their order.
            if (DateOnly.FromDateTime(entry.WatchedAt) != watchedOn)
            {
                entry.WatchedAt = DiaryExtensions.ToWatchedAt(watchedOn);
            }

            entry.Rating = dto.Rating;
            entry.Liked = dto.Liked;
            entry.Rewatched = dto.Rewatched;

            await _context.SaveChangesAsync();
            await _context.SyncWatchedStatusAsync(userId, entry.MovieId);
            await _context.SaveChangesAsync();

            return Ok(new DiaryEntryDto
            {
                Id = entry.Id,
                TmdbId = entry.Movie.TmdbId,
                Title = entry.Movie.Title,
                PosterUrl = entry.Movie.PosterUrl,
                Rating = entry.Rating,
                Liked = entry.Liked,
                Rewatched = entry.Rewatched,
                HasReview = await _context.Reviews.AnyAsync(r => r.DiaryEntryId == entry.Id),
                WatchedAt = entry.WatchedAt
            });
        }

        [HttpDelete("api/diary/{entryId:int}")]
        [EndpointSummary("Delete one of the current user's diary entries. Its review goes too, and deleting the last one marks the film unwatched")]
        public async Task<IActionResult> DeleteEntry(int entryId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var entry = await _context.DiaryEntries
                .FirstOrDefaultAsync(d => d.Id == entryId && d.UserId == userId);

            if (entry == null)
            {
                return NotFound();
            }

            await _context.DeleteDiaryEntriesAsync([entry]);
            await _context.SaveChangesAsync();
            await _context.SyncWatchedStatusAsync(userId, entry.MovieId);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
