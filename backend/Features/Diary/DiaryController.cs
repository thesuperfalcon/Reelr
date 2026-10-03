using backend.Data;
using backend.Features.Diary.DTOs;
using backend.Features.Movies;
using backend.Features.Ratings;
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

            var entries = await _context.DiaryEntries
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
                    WatchedAt = d.WatchedAt
                })
                .ToListAsync();

            return Ok(entries);
        }

        [HttpPost("api/movies/{tmdbId:int}/diary")]
        [EndpointSummary("Save rating and status for a movie and log it as one diary entry")]
        public async Task<ActionResult<DiaryEntryDto>> LogEntry(int tmdbId, LogDiaryEntryDto dto)
        {
            if (dto.Score is decimal score && decimal.Remainder(score * 2, 1) != 0)
            {
                return BadRequest("Score must be in increments of 0.5.");
            }

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
                    WatchedAt = DateTime.UtcNow
                };

                _context.WatchedMovies.Add(status);
            }

            status.Liked = dto.Liked;
            status.Rewatched = dto.Rewatched;

            var entry = _context.LogDiaryEntry(status, rating?.Score);
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
                WatchedAt = entry.WatchedAt
            });
        }
    }
}
