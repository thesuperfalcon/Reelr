using backend.Data;
using backend.Features.Auth;
using backend.Features.Movies;
using backend.Features.Reviews.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.Reviews
{
    [ApiController]
    [Route("api/movies/{tmdbId:int}/reviews")]
    public class ReviewController : ControllerBase
    {
        private readonly ReelrContext _context;
        private readonly MovieCatalog _movieCatalog;

        public ReviewController(ReelrContext context, MovieCatalog movieCatalog)
        {
            _context = context;
            _movieCatalog = movieCatalog;
        }

        [HttpGet]
        [EndpointSummary("Get reviews for a movie")]
        public async Task<ActionResult<List<ReviewDto>>> GetReviews(int tmdbId)
        {
            var reviews = await ToDtos(_context.Reviews.Where(r => r.Movie.TmdbId == tmdbId))
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return Ok(reviews);
        }

        [HttpGet("/api/users/{userId:int}/reviews")]
        [EndpointSummary("Get reviews by a user")]
        public async Task<ActionResult<List<ReviewDto>>> GetReviewsByUser(int userId)
        {
            var reviews = await ToDtos(_context.Reviews.Where(r => r.UserId == userId))
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return Ok(reviews);
        }

        [HttpGet("/api/reviews/{id:int}")]
        [EndpointSummary("Get a review")]
        public async Task<ActionResult<ReviewDto>> GetReview(int id)
        {
            var review = await ToDtos(_context.Reviews.Where(r => r.Id == id)).FirstOrDefaultAsync();

            if (review == null)
            {
                return NotFound();
            }

            return Ok(review);
        }

        [HttpPost]
        [Authorize]
        [EndpointSummary("Add a review to a movie")]
        public async Task<ActionResult<ReviewDto>> CreateReview(int tmdbId, CreateReviewDto dto)
        {
            var userId = User.GetUserId();

            var movie = await _movieCatalog.GetOrCreateAsync(tmdbId);

            if (movie == null)
            {
                return NotFound();
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
                ContainsSpoilers = dto.ContainsSpoilers,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            return Ok(await ToDtos(_context.Reviews.Where(r => r.Id == review.Id)).SingleAsync());
        }

        [HttpPut("/api/reviews/{id:int}")]
        [Authorize]
        [EndpointSummary("Update a review")]
        public async Task<ActionResult<ReviewDto>> UpdateReview(int id, UpdateReviewDto dto)
        {
            var userId = User.GetUserId();

            var review = await _context.Reviews
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            if (review == null)
            {
                return NotFound();
            }

            if (dto.Text != null)
            {
                if (string.IsNullOrWhiteSpace(dto.Text))
                {
                    ModelState.AddModelError(nameof(dto.Text), "Review text cannot be blank.");
                    return ValidationProblem(ModelState);
                }

                // Only a text change marks the review as edited.
                if (dto.Text != review.Text)
                {
                    review.Text = dto.Text;
                    review.UpdatedAt = DateTime.UtcNow;
                }
            }

            if (dto.ContainsSpoilers is bool containsSpoilers)
            {
                review.ContainsSpoilers = containsSpoilers;
            }

            await _context.SaveChangesAsync();

            return Ok(await ToDtos(_context.Reviews.Where(r => r.Id == id)).SingleAsync());
        }

        [HttpDelete("/api/reviews/{id:int}")]
        [Authorize]
        [EndpointSummary("Delete a review")]
        public async Task<IActionResult> DeleteReview(int id)
        {
            var userId = User.GetUserId();

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

        // Every review response has the same shape: author, film, the author's current rating,
        // the watch date of the diary entry that logged the review, and its likes and comments.
        // The endpoints are public; a valid token only adds whether the caller likes each review.
        private IQueryable<ReviewDto> ToDtos(IQueryable<Review> reviews)
        {
            var viewerId = User.FindUserId();

            return
                from r in reviews
                join rating in _context.Ratings
                    on new { r.UserId, r.MovieId } equals new { rating.UserId, rating.MovieId } into ratings
                from rating in ratings.DefaultIfEmpty()
                select new ReviewDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    Username = r.User.UserName ?? string.Empty,
                    ProfileImageUrl = r.User.ProfileImageUrl,
                    TmdbId = r.Movie.TmdbId,
                    Title = r.Movie.Title,
                    PosterUrl = r.Movie.PosterUrl,
                    Text = r.Text,
                    ContainsSpoilers = r.ContainsSpoilers,
                    LikeCount = r.Likes.Count,
                    CommentCount = r.Comments.Count,
                    LikedByMe = viewerId != null && r.Likes.Any(l => l.UserId == viewerId),
                    Score = rating == null ? (decimal?)null : rating.Score,
                    WatchedAt = r.DiaryEntry == null ? (DateTime?)null : r.DiaryEntry.WatchedAt,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                };
        }
    }
}
