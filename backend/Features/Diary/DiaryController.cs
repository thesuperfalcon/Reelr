using backend.Data;
using backend.Features.Auth;
using backend.Features.Diary.DTOs;
using backend.Features.Movies;
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
        [EndpointSummary("Get the current user's diary, newest entry first")]
        public async Task<ActionResult<List<DiaryEntryDto>>> GetDiary()
        {
            var userId = User.GetUserId();

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
