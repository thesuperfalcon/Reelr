using backend.Data;
using backend.Features.Auth;
using backend.Features.Movies;
using backend.Features.Paging;
using backend.Features.Users;
using backend.Features.WatchlistItems.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.WatchlistItems
{
    [Authorize]
    [ApiController]
    [Route("api/watchlist")]
    public class WatchlistController : ControllerBase
    {
        private readonly ReelrContext _context;
        private readonly MovieCatalog _movieCatalog;

        public WatchlistController(ReelrContext context, MovieCatalog movieCatalog)
        {
            _context = context;
            _movieCatalog = movieCatalog;
        }

        [HttpPost("{tmdbId:int}")]
        [EndpointSummary("Add a movie to the current user's watchlist")]
        public async Task<IActionResult> AddToWatchlist(int tmdbId)
        {
            var userId = User.GetUserId();

            var movie = await _movieCatalog.GetOrCreateAsync(tmdbId);

            if (movie == null)
            {
                return NotFound();
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
            var userId = User.GetUserId();

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
        [EndpointSummary("Get the current user's watchlist, newest first, one page at a time")]
        public async Task<ActionResult<PageDto<WatchlistEntryDto>>> GetWatchlist([FromQuery] PageQuery page)
        {
            var userId = User.GetUserId();

            return Ok(await GetEntries(userId, page));
        }

        [HttpGet("{tmdbId:int}")]
        [EndpointSummary("Get whether a movie is on the current user's watchlist")]
        public async Task<ActionResult<WatchlistStateDto>> GetWatchlistState(int tmdbId)
        {
            var userId = User.GetUserId();

            return Ok(new WatchlistStateDto
            {
                OnWatchlist = await _context.WatchlistItems.AnyAsync(w => w.UserId == userId && w.Movie.TmdbId == tmdbId)
            });
        }

        [AllowAnonymous]
        [HttpGet("/api/users/{userId:int}/watchlist")]
        [EndpointSummary("Get a user's watchlist, if their watchlist visibility allows the caller to see it")]
        public async Task<ActionResult<PageDto<WatchlistEntryDto>>> GetUserWatchlist(int userId, [FromQuery] PageQuery page)
        {
            var viewerId = User.FindUserId();

            // A hidden watchlist answers like a missing user, so it does not reveal that it exists.
            if (await _context.CanSeeWatchlistAsync(viewerId, userId) != true)
            {
                return NotFound();
            }

            return Ok(await GetEntries(userId, page));
        }

        private async Task<PageDto<WatchlistEntryDto>> GetEntries(int userId, PageQuery page)
        {
            var items = _context.WatchlistItems.Where(w => w.UserId == userId);

            if (page.After is PageCursor after)
            {
                var at = after.At;
                var id = after.Id;
                items = items.Where(w => w.AddedAt < at || (w.AddedAt == at && w.MovieId < id));
            }

            var rows = await items
                .OrderByDescending(w => w.AddedAt)
                .ThenByDescending(w => w.MovieId)
                .Take(page.Limit + 1)
                .Select(w => new
                {
                    w.MovieId,
                    Entry = new WatchlistEntryDto
                    {
                        TmdbId = w.Movie.TmdbId,
                        Title = w.Movie.Title,
                        PosterUrl = w.Movie.PosterUrl,
                        AddedAt = w.AddedAt
                    }
                })
                .ToListAsync();

            return PageDto.From(rows, page.Limit, r => new PageCursor(r.Entry.AddedAt, r.MovieId), r => r.Entry);
        }
    }
}
