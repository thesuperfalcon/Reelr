using backend.Data;
using backend.Features.Movies;
using backend.Features.Ratings.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace backend.Features.Ratings
{
    [Authorize]
    [ApiController]
    [Route("api/movies/{tmdbId:int}/rating")]
    public class RatingController : ControllerBase
    {
        private readonly ReelrContext _context;
        private readonly TmdbService _tmdbService;

        public RatingController(ReelrContext context, TmdbService tmdbService)
        {
            _context = context;
            _tmdbService = tmdbService;
        }

        [HttpGet]
        [EndpointSummary("Get the current user's rating for a movie")]
        public async Task<ActionResult<RatingDto>> GetRating(int tmdbId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var rating = await _context.Ratings
                .FirstOrDefaultAsync(r => r.UserId == userId && r.Movie.TmdbId == tmdbId);

            if (rating == null)
            {
                return NotFound();
            }

            rating.Score = dto.Score;

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
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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

        private static bool IsHalfStep(decimal score)
        {
            return decimal.Remainder(score * 2, 1) == 0;
        }
    }
}
