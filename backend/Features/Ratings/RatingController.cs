using backend.Data;
using backend.Features.Auth;
using backend.Features.Diary;
using backend.Features.Movies;
using backend.Features.Ratings.DTOs;
using backend.Features.WatchedMovies;
using backend.Features.WatchlistItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.Ratings
{
    [Authorize]
    [ApiController]
    [Route("api/movies/{tmdbId:int}/rating")]
    public class RatingController : ControllerBase
    {
        private readonly ReelrContext _context;
        private readonly MovieCatalog _movieCatalog;

        public RatingController(ReelrContext context, MovieCatalog movieCatalog)
        {
            _context = context;
            _movieCatalog = movieCatalog;
        }

        [HttpGet]
        [EndpointSummary("Get the current user's rating for a movie")]
        public async Task<ActionResult<RatingDto>> GetRating(int tmdbId)
        {
            var userId = User.GetUserId();

            var rating = await _context.Ratings
                .FirstOrDefaultAsync(r => r.UserId == userId && r.Movie.TmdbId == tmdbId);

            if (rating == null)
            {
                return NotFound();
            }

            return Ok(new RatingDto
            {
                TmdbId = tmdbId,
                Score = rating.Score
            });
        }

        [HttpPost]
        [EndpointSummary("Rate a movie")]
        public async Task<ActionResult<RatingDto>> CreateRating(int tmdbId, CreateRatingDto dto)
        {
            if (!IsHalfStep(dto.Score))
            {
                return BadRequest("Score must be in increments of 0.5.");
            }

            var userId = User.GetUserId();

            var movie = await _movieCatalog.GetOrCreateAsync(tmdbId);

            if (movie == null)
            {
                return NotFound();
            }

            var alreadyRated = await _context.Ratings
                .AnyAsync(r => r.UserId == userId && r.MovieId == movie.Id);

            if (alreadyRated)
            {
                return Conflict("User has already rated this movie.");
            }

            var rating = new Rating
            {
                UserId = userId,
                MovieId = movie.Id,
                Score = dto.Score
            };

            _context.Ratings.Add(rating);
            await LogRatingAsync(userId, movie.Id, rating.Score);
            await _context.SaveChangesAsync();

            return Ok(new RatingDto
            {
                TmdbId = tmdbId,
                Score = rating.Score
            });
        }

        [HttpPut]
        [EndpointSummary("Update the current user's rating for a movie")]
        public async Task<ActionResult<RatingDto>> UpdateRating(int tmdbId, UpdateRatingDto dto)
        {
            if (!IsHalfStep(dto.Score))
            {
                return BadRequest("Score must be in increments of 0.5.");
            }

            var userId = User.GetUserId();

            var rating = await _context.Ratings
                .FirstOrDefaultAsync(r => r.UserId == userId && r.Movie.TmdbId == tmdbId);

            if (rating == null)
            {
                return NotFound();
            }

            rating.Score = dto.Score;

            await LogRatingAsync(userId, rating.MovieId, rating.Score);
            await _context.SaveChangesAsync();

            return Ok(new RatingDto
            {
                TmdbId = tmdbId,
                Score = rating.Score
            });
        }

        [HttpDelete]
        [EndpointSummary("Remove the current user's rating for a movie")]
        public async Task<IActionResult> DeleteRating(int tmdbId)
        {
            var userId = User.GetUserId();

            var rating = await _context.Ratings
                .FirstOrDefaultAsync(r => r.UserId == userId && r.Movie.TmdbId == tmdbId);

            if (rating == null)
            {
                return NotFound();
            }

            _context.Ratings.Remove(rating);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // A rated film counts as watched, so each rating logs a new diary entry and the film leaves the watchlist.
        private async Task LogRatingAsync(int userId, int movieId, decimal score)
        {
            await _context.RemoveWatchedFromWatchlistAsync(userId, movieId);

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

            _context.LogDiaryEntry(status, score);
        }

        private static bool IsHalfStep(decimal score)
        {
            return decimal.Remainder(score * 2, 1) == 0;
        }
    }
}
