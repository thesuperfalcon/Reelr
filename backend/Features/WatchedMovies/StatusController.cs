using backend.Data;
using backend.Features.Movies;
using backend.Features.WatchedMovies.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace backend.Features.WatchedMovies
{
    [Authorize]
    [ApiController]
    [Route("api/movies/{tmdbId:int}/status")]
    public class StatusController : ControllerBase
    {
        private readonly ReelrContext _context;
        private readonly TmdbService _tmdbService;

        public StatusController(ReelrContext context, TmdbService tmdbService)
        {
            _context = context;
            _tmdbService = tmdbService;
        }

        [HttpGet("/api/watched")]
        [EndpointSummary("Get all movies the current user has watched")]
        public async Task<IActionResult> GetWatchedMovies()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var watched = await _context.WatchedMovies
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.WatchedAt)
                .Select(w => new
                {
                    w.Movie.TmdbId,
                    w.Movie.Title,
                    w.Movie.PosterUrl,
                    w.Liked,
                    w.Rewatched,
                    w.WatchedAt
                })
                .ToListAsync();

            return Ok(watched);
        }

        [HttpGet]
        [EndpointSummary("Get the current user's watched status for a movie")]
        public async Task<ActionResult<StatusDto>> GetStatus(int tmdbId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var status = await _context.WatchedMovies
                .FirstOrDefaultAsync(w => w.UserId == userId && w.Movie.TmdbId == tmdbId);

            if (status == null)
            {
                return NotFound();
            }

            status.Liked = dto.Liked;
            status.Rewatched = dto.Rewatched;

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
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var status = await _context.WatchedMovies
                .FirstOrDefaultAsync(w => w.UserId == userId && w.Movie.TmdbId == tmdbId);

            if (status == null)
            {
                return NotFound();
            }

            _context.WatchedMovies.Remove(status);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
