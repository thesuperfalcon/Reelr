using backend.Data;
using backend.Features.Auth;
using backend.Features.Diary;
using backend.Features.Movies;
using backend.Features.Paging;
using backend.Features.Reviews.DTOs;
using backend.Features.Users;
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
        private readonly JournalService _journal;

        public ReviewController(ReelrContext context, MovieCatalog movieCatalog, JournalService journal)
        {
            _context = context;
            _movieCatalog = movieCatalog;
            _journal = journal;
        }

        [HttpGet]
        [EndpointSummary("Get reviews for a movie, newest first, one page at a time")]
        public async Task<ActionResult<MovieReviewPageDto>> GetReviews(int tmdbId, [FromQuery] PageQuery page)
        {
            // People the viewer blocked are left out.
            var blocked = _context.BlockedBy(User.FindUserId());
            var reviews = _context.Reviews.Where(r => r.Movie.TmdbId == tmdbId && !blocked.Contains(r.UserId));
            var result = await GetPage(reviews, page);

            return Ok(new MovieReviewPageDto
            {
                Items = result.Items,
                NextCursor = result.NextCursor,
                TotalCount = await reviews.CountAsync()
            });
        }

        [Authorize]
        [HttpGet("mine")]
        [EndpointSummary("Get the current user's review of a movie. 204 when they have not reviewed it")]
        public async Task<ActionResult<ReviewDto?>> GetMyReview(int tmdbId)
        {
            var userId = User.GetUserId();

            return Ok(await ToDtos(_context.Reviews.Where(r => r.Movie.TmdbId == tmdbId && r.UserId == userId)).FirstOrDefaultAsync());
        }

        [HttpGet("/api/users/{userId:int}/reviews")]
        [EndpointSummary("Get reviews by a user, newest first, one page at a time")]
        public async Task<ActionResult<PageDto<ReviewDto>>> GetReviewsByUser(int userId, [FromQuery] PageQuery page)
        {
            return Ok(await GetPage(_context.Reviews.Where(r => r.UserId == userId), page));
        }

        private async Task<PageDto<ReviewDto>> GetPage(IQueryable<Review> reviews, PageQuery page)
        {
            if (page.After is PageCursor after)
            {
                var at = after.At;
                var id = after.Id;
                reviews = reviews.Where(r => r.CreatedAt < at || (r.CreatedAt == at && r.Id < id));
            }

            var rows = await ToDtos(reviews)
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.Id)
                .Take(page.Limit + 1)
                .ToListAsync();

            return PageDto.From(rows, page.Limit, r => new PageCursor(r.CreatedAt, r.Id));
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
        [EndpointSummary("Add a review to a movie without logging it. On a watched movie it belongs to the latest diary entry")]
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

            var review = await _journal.AddReviewAsync(userId, movie.Id, dto.Text, dto.ContainsSpoilers);

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
            var diaryVisible = _context.DiaryOwnersVisibleTo(viewerId);

            return
                from r in reviews
                join rating in _context.Ratings
                    on new { r.UserId, r.MovieId } equals new { rating.UserId, rating.MovieId } into ratings
                from rating in ratings.DefaultIfEmpty()
                // The rating and watch date are diary data, so they follow the author's diary visibility.
                let canSeeDiary = diaryVisible.Contains(r.UserId)
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
                    Score = rating == null || !canSeeDiary ? (decimal?)null : rating.Score,
                    WatchedAt = r.DiaryEntry == null || !canSeeDiary ? (DateTime?)null : r.DiaryEntry.WatchedAt,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                };
        }
    }
}
