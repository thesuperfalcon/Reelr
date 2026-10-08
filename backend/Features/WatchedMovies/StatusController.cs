using backend.Data;
using backend.Features.Auth;
using backend.Features.Diary;
using backend.Features.Movies;
using backend.Features.WatchedMovies.DTOs;
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
        private readonly JournalService _journal;

        public StatusController(ReelrContext context, MovieCatalog movieCatalog, JournalService journal)
        {
            _context = context;
            _movieCatalog = movieCatalog;
            _journal = journal;
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

            var status = await _journal.MarkWatchedAsync(userId, movie.Id, dto.Liked, dto.Rewatched);

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

            await _journal.MarkWatchedAsync(userId, status.MovieId, dto.Liked, dto.Rewatched);

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

            await _journal.UnwatchAsync(status);

            return NoContent();
        }
    }
}
