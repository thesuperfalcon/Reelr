using backend.Data;
using backend.Features.Auth;
using backend.Features.Diary.DTOs;
using backend.Features.Movies;
using backend.Features.Paging;
using backend.Features.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.Diary
{
    [Authorize]
    [ApiController]
    public class DiaryController : ControllerBase
    {
        private readonly ReelrContext _context;
        private readonly MovieCatalog _movieCatalog;
        private readonly JournalService _journal;

        public DiaryController(ReelrContext context, MovieCatalog movieCatalog, JournalService journal)
        {
            _context = context;
            _movieCatalog = movieCatalog;
            _journal = journal;
        }

        [HttpGet("api/watched")]
        [EndpointSummary("Get the current user's diary, newest entry first, one page at a time")]
        public async Task<ActionResult<PageDto<DiaryEntryDto>>> GetDiary([FromQuery] PageQuery page)
        {
            var userId = User.GetUserId();

            return Ok(await GetEntries(userId, page));
        }

        [HttpGet("api/watched/films")]
        [EndpointSummary("Get the TMDB ids of every film the current user has watched")]
        public async Task<ActionResult<List<int>>> GetWatchedFilms()
        {
            var userId = User.GetUserId();

            // One number per film, so pages can mark watched films without reading the whole diary.
            return Ok(await _context.WatchedMovies
                .Where(w => w.UserId == userId)
                .Select(w => w.Movie.TmdbId)
                .ToListAsync());
        }

        [AllowAnonymous]
        [HttpGet("api/users/{userId:int}/diary")]
        [EndpointSummary("Get a user's diary, newest entry first, if their diary visibility allows the caller to see it")]
        public async Task<ActionResult<PageDto<DiaryEntryDto>>> GetUserDiary(int userId, [FromQuery] PageQuery page)
        {
            // A hidden diary answers like a missing user, so it does not reveal that it exists.
            if (await _context.CanSeeDiaryAsync(User.FindUserId(), userId) != true)
            {
                return NotFound();
            }

            return Ok(await GetEntries(userId, page));
        }

        private async Task<PageDto<DiaryEntryDto>> GetEntries(int userId, PageQuery page)
        {
            var entries = _context.DiaryEntries.Where(d => d.UserId == userId);

            if (page.After is PageCursor after)
            {
                var at = after.At;
                var id = after.Id;
                entries = entries.Where(d => d.WatchedAt < at || (d.WatchedAt == at && d.Id < id));
            }

            var rows = await entries
                .OrderByDescending(d => d.WatchedAt)
                .ThenByDescending(d => d.Id)
                .Take(page.Limit + 1)
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

            return PageDto.From(rows, page.Limit, e => new PageCursor(e.WatchedAt, e.Id));
        }

        [HttpPost("api/movies/{tmdbId:int}/diary")]
        [EndpointSummary("Save rating, status and review for a movie and log it as one diary entry")]
        public async Task<ActionResult<DiaryEntryDto>> LogEntry(int tmdbId, LogDiaryEntryDto dto)
        {
            if (dto.Score is decimal score && !JournalService.IsHalfStep(score))
            {
                return BadRequest("Score must be in increments of 0.5.");
            }

            if (dto.WatchedOn is DateOnly watchedOn && JournalService.ValidateWatchedOn(watchedOn) is string dateError)
            {
                return BadRequest(dateError);
            }

            var watchedAt = dto.WatchedOn is DateOnly day ? JournalService.ToWatchedAt(day) : DateTime.UtcNow;
            var userId = User.GetUserId();

            var movie = await _movieCatalog.GetOrCreateAsync(tmdbId);

            if (movie == null)
            {
                return NotFound();
            }

            var (entry, reviewWritten) = await _journal.LogAsync(
                userId, movie.Id, watchedAt, dto.Score, dto.Liked, dto.Rewatched, dto.Review, dto.ContainsSpoilers);

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
            if (dto.Rating is decimal score && !JournalService.IsHalfStep(score))
            {
                return BadRequest("Rating must be in increments of 0.5.");
            }

            var watchedOn = dto.WatchedOn!.Value;

            if (JournalService.ValidateWatchedOn(watchedOn) is string dateError)
            {
                return BadRequest(dateError);
            }

            var userId = User.GetUserId();

            var entry = await _context.DiaryEntries
                .Include(d => d.Movie)
                .FirstOrDefaultAsync(d => d.Id == entryId && d.UserId == userId);

            if (entry == null)
            {
                return NotFound();
            }

            await _journal.EditEntryAsync(entry, watchedOn, dto.Rating, dto.Liked, dto.Rewatched);

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
            var userId = User.GetUserId();

            var entry = await _context.DiaryEntries
                .FirstOrDefaultAsync(d => d.Id == entryId && d.UserId == userId);

            if (entry == null)
            {
                return NotFound();
            }

            await _journal.DeleteEntryAsync(entry);

            return NoContent();
        }
    }
}
