using backend.Data;
using backend.Features.Movies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace backend.Features.WatchlistItems
{
    [Authorize]
    [ApiController]
    [Route("api/watchlist")]
    public class WatchlistController : ControllerBase
    {
        private readonly ReelrContext _context;
        private readonly TmdbService _tmdbService;

        public WatchlistController(ReelrContext context, TmdbService tmdbService)
        {
            _context = context;
            _tmdbService = tmdbService;
        }

        [HttpPost("{tmdbId:int}")]
        [EndpointSummary("Add a movie to the current user's watchlist")]
        public async Task<IActionResult> AddToWatchlist(int tmdbId)
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

            var alreadyOnWatchlist = await _context.WatchlistItems
                .AnyAsync(w => w.UserId == userId && w.MovieId == movie.Id);

            if (alreadyOnWatchlist)
            {
                return Conflict("Movie is already on the watchlist.");
            }

            _context.WatchlistItems.Add(new WatchlistItem
            {
                UserId = userId,
                MovieId = movie.Id,
                AddedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpDelete("{tmdbId:int}")]
        [EndpointSummary("Remove a movie from the current user's watchlist")]
        public async Task<IActionResult> RemoveFromWatchlist(int tmdbId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var item = await _context.WatchlistItems
                .FirstOrDefaultAsync(w => w.UserId == userId && w.Movie.TmdbId == tmdbId);

            if (item == null)
            {
                return NotFound();
            }

            _context.WatchlistItems.Remove(item);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet]
        [EndpointSummary("Get the current user's watchlist")]
        public async Task<IActionResult> GetWatchlist()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var watchlist = await _context.WatchlistItems
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.AddedAt)
                .Select(w => new
                {
                    w.Movie.TmdbId,
                    w.Movie.Title,
                    w.Movie.PosterUrl,
                    w.AddedAt
                })
                .ToListAsync();

            return Ok(watchlist);
        }
    }
}
