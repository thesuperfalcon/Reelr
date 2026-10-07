using backend.Data;
using backend.Features.Auth;
using backend.Features.Diary;
using backend.Features.Movies;
using backend.Features.Ratings.DTOs;
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
        private readonly JournalService _journal;

        public RatingController(ReelrContext context, MovieCatalog movieCatalog, JournalService journal)
        {
            _context = context;
            _movieCatalog = movieCatalog;
            _journal = journal;
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
            if (!JournalService.IsHalfStep(dto.Score))
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

            var rating = await _journal.RateAsync(userId, movie.Id, dto.Score);

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
            if (!JournalService.IsHalfStep(dto.Score))
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

            await _journal.RateAsync(userId, rating.MovieId, dto.Score);

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
    }
}
