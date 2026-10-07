using backend.Data;
using backend.Features.Auth;
using backend.Features.Diary;
using backend.Features.Movies;
using backend.Features.WatchedMovies.DTOs;
using backend.Features.WatchlistItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.WatchedMovies
{
    [Authorize]
    [ApiController]
    [Route("api/movies/{tmdbId:int}/status")]
    public class StatusController : ControllerBase
    {
        private readonly ReelrContext _context;
        private readonly MovieCatalog _movieCatalog;

        public StatusController(ReelrContext context, MovieCatalog movieCatalog)
        {
            _context = context;
            _movieCatalog = movieCatalog;
        }

        [HttpGet]
        [EndpointSummary("Get the current user's watched status for a movie")]
        public async Task<ActionResult<StatusDto>> GetStatus(int tmdbId)
        {
            var userId = User.GetUserId();

            var status = await _context.WatchedMovies
                .FirstOrDefaultAsync(w => w.UserId == userId && w.Movie.TmdbId == tmdbId);

            if (status == null)
            {
                return NotFound();
            }

            return Ok(new StatusDto
            {
                TmdbId = tmdbId,
                Liked = status.Liked,
                Rewatched = status.Rewatched,
                WatchedAt = status.WatchedAt
            });
        }

        [HttpPost]
        [EndpointSummary("Mark a movie as watched for the current user")]
        public async Task<ActionResult<StatusDto>> CreateStatus(int tmdbId, CreateStatusDto dto)
        {
            var userId = User.GetUserId();

            var movie = await _movieCatalog.GetOrCreateAsync(tmdbId);

            if (movie == null)
            {
                return NotFound();
            }

            var alreadyWatched = await _context.WatchedMovies
                .AnyAsync(w => w.UserId == userId && w.MovieId == movie.Id);

            if (alreadyWatched)
            {
                return Conflict("Movie status already exists for this user.");
            }

            var status = new WatchedMovie
            {
                UserId = userId,
                MovieId = movie.Id,
                WatchedAt = DateTime.UtcNow,
                Liked = dto.Liked,
                Rewatched = dto.Rewatched
            };

            _context.WatchedMovies.Add(status);
            _context.LogDiaryEntry(status, await CurrentRatingAsync(userId, movie.Id));
            await _context.RemoveWatchedFromWatchlistAsync(userId, movie.Id);
            await _context.SaveChangesAsync();

            return Ok(new StatusDto
            {
                TmdbId = tmdbId,
                Liked = status.Liked,
                Rewatched = status.Rewatched,
                WatchedAt = status.WatchedAt
            });
        }

        [HttpPut]
        [EndpointSummary("Update the current user's watched status for a movie")]
        public async Task<ActionResult<StatusDto>> UpdateStatus(int tmdbId, UpdateStatusDto dto)
        {
            var userId = User.GetUserId();

            var status = await _context.WatchedMovies
                .FirstOrDefaultAsync(w => w.UserId == userId && w.Movie.TmdbId == tmdbId);

            if (status == null)
            {
                return NotFound();
            }

            status.Liked = dto.Liked;
            status.Rewatched = dto.Rewatched;

            _context.LogDiaryEntry(status, await CurrentRatingAsync(userId, status.MovieId));
            await _context.RemoveWatchedFromWatchlistAsync(userId, status.MovieId);
            await _context.SaveChangesAsync();

            return Ok(new StatusDto
            {
                TmdbId = tmdbId,
                Liked = status.Liked,
                Rewatched = status.Rewatched,
                WatchedAt = status.WatchedAt
            });
        }

        [HttpDelete]
        [EndpointSummary("Remove the current user's watched status for a movie")]
        public async Task<IActionResult> DeleteStatus(int tmdbId)
        {
            var userId = User.GetUserId();

            var status = await _context.WatchedMovies
                .FirstOrDefaultAsync(w => w.UserId == userId && w.Movie.TmdbId == tmdbId);

            if (status == null)
            {
                return NotFound();
            }

            // An unwatched film has no viewings, so its diary entries and the reviews they logged go too.
            await _context.DeleteDiaryEntriesAsync(await _context.DiaryEntries
                .Where(d => d.UserId == userId && d.MovieId == status.MovieId)
                .ToListAsync());

            _context.WatchedMovies.Remove(status);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private Task<decimal?> CurrentRatingAsync(int userId, int movieId)
        {
            return _context.Ratings
                .Where(r => r.UserId == userId && r.MovieId == movieId)
                .Select(r => (decimal?)r.Score)
                .FirstOrDefaultAsync();
        }
    }
}
