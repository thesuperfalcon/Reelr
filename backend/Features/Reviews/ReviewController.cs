using backend.Data;
using backend.Features.Movies;
using backend.Features.Reviews.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace backend.Features.Reviews
{
    [ApiController]
    [Route("api/movies/{tmdbId:int}/reviews")]
    public class ReviewController : ControllerBase
    {
        private readonly ReelrContext _context;
        private readonly TmdbService _tmdbService;

        public ReviewController(ReelrContext context, TmdbService tmdbService)
        {
            _context = context;
            _tmdbService = tmdbService;
        }

        [HttpGet]
        [EndpointSummary("Get reviews for a movie")]
        public async Task<ActionResult<List<ReviewDto>>> GetReviews(int tmdbId)
        {
            var reviews = await (
                from r in _context.Reviews
                where r.Movie.TmdbId == tmdbId
                join rating in _context.Ratings
                    on new { r.UserId, r.MovieId } equals new { rating.UserId, rating.MovieId } into ratings
                from rating in ratings.DefaultIfEmpty()
                orderby r.CreatedAt descending
                select new ReviewDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    Username = r.User.UserName ?? string.Empty,
                    TmdbId = tmdbId,
                    Text = r.Text,
                    Score = rating == null ? (decimal?)null : rating.Score,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                }).ToListAsync();

            return Ok(reviews);
        }

        [HttpGet("/api/users/{userId:int}/reviews")]
        [EndpointSummary("Get reviews by a user")]
        public async Task<ActionResult<List<ReviewDto>>> GetReviewsByUser(int userId)
        {
            var reviews = await (
                from r in _context.Reviews
                where r.UserId == userId
                join rating in _context.Ratings
                    on new { r.UserId, r.MovieId } equals new { rating.UserId, rating.MovieId } into ratings
                from rating in ratings.DefaultIfEmpty()
                orderby r.CreatedAt descending
                select new ReviewDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    Username = r.User.UserName ?? string.Empty,
                    TmdbId = r.Movie.TmdbId,
                    Text = r.Text,
                    Score = rating == null ? (decimal?)null : rating.Score,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                }).ToListAsync();

            return Ok(reviews);
        }

        [HttpPost]
        [Authorize]
        [EndpointSummary("Add a review to a movie")]
        public async Task<ActionResult<ReviewDto>> CreateReview(int tmdbId, CreateReviewDto dto)
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

            var alreadyReviewed = await _context.Reviews
                .AnyAsync(r => r.UserId == userId && r.MovieId == movie.Id);

            if (alreadyReviewed)
            {
                return Conflict("User has already reviewed this movie.");
            }

            var review = new Review
            {
                UserId = userId,
                MovieId = movie.Id,
                Text = dto.Text,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            await _context.Entry(review).Reference(r => r.User).LoadAsync();

            return Ok(new ReviewDto
            {
                Id = review.Id,
                UserId = review.UserId,
                Username = review.User.UserName ?? string.Empty,
                TmdbId = tmdbId,
                Text = review.Text,
                Score = await GetScore(userId, movie.Id),
                CreatedAt = review.CreatedAt,
                UpdatedAt = review.UpdatedAt
            });
        }

        [HttpPut("/api/reviews/{id:int}")]
        [Authorize]
        [EndpointSummary("Update a review")]
        public async Task<ActionResult<ReviewDto>> UpdateReview(int id, UpdateReviewDto dto)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var review = await _context.Reviews
                .Include(r => r.Movie)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            if (review == null)
            {
                return NotFound();
            }

            review.Text = dto.Text;
            review.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new ReviewDto
            {
                Id = review.Id,
                UserId = review.UserId,
                Username = review.User.UserName ?? string.Empty,
                TmdbId = review.Movie.TmdbId,
                Text = review.Text,
                Score = await GetScore(userId, review.MovieId),
                CreatedAt = review.CreatedAt,
                UpdatedAt = review.UpdatedAt
            });
        }

        [HttpDelete("/api/reviews/{id:int}")]
        [Authorize]
        [EndpointSummary("Delete a review")]
        public async Task<IActionResult> DeleteReview(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var review = await _context.Reviews
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            if (review == null)
            {
                return NotFound();
            }

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task<decimal?> GetScore(int userId, int movieId)
        {
            var rating = await _context.Ratings
                .FirstOrDefaultAsync(r => r.UserId == userId && r.MovieId == movieId);

            return rating?.Score;
        }
    }
}
