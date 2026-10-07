using backend.Data;
using backend.Features.Reviews.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace backend.Features.Reviews
{
    [ApiController]
    public class ReviewCommentController : ControllerBase
    {
        private readonly ReelrContext _context;

        public ReviewCommentController(ReelrContext context)
        {
            _context = context;
        }

        [HttpGet("api/reviews/{reviewId:int}/comments")]
        [EndpointSummary("Get the comments on a review, oldest first")]
        public async Task<ActionResult<List<ReviewCommentDto>>> GetComments(int reviewId)
        {
            if (!await _context.Reviews.AnyAsync(r => r.Id == reviewId))
            {
                return NotFound();
            }

            var comments = await ToDtos(_context.ReviewComments.Where(c => c.ReviewId == reviewId))
                .OrderBy(c => c.CreatedAt)
                .ThenBy(c => c.Id)
                .ToListAsync();

            return Ok(comments);
        }

        [HttpPost("api/reviews/{reviewId:int}/comments")]
        [Authorize]
        [EndpointSummary("Comment on a review")]
        public async Task<ActionResult<ReviewCommentDto>> CreateComment(int reviewId, CreateReviewCommentDto dto)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (!await _context.Reviews.AnyAsync(r => r.Id == reviewId))
            {
                return NotFound();
            }

            var comment = new ReviewComment
            {
                ReviewId = reviewId,
                UserId = userId,
                Text = dto.Text.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.ReviewComments.Add(comment);
            await _context.SaveChangesAsync();

            return Ok(await ToDtos(_context.ReviewComments.Where(c => c.Id == comment.Id)).SingleAsync());
        }

        [HttpPut("api/comments/{id:int}")]
        [Authorize]
        [EndpointSummary("Edit a comment the current user wrote")]
        public async Task<ActionResult<ReviewCommentDto>> UpdateComment(int id, UpdateReviewCommentDto dto)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var comment = await _context.ReviewComments.FirstOrDefaultAsync(c => c.Id == id);

            if (comment == null)
            {
                return NotFound();
            }

            if (comment.UserId != userId)
            {
                return Forbid();
            }

            var text = dto.Text.Trim();

            if (text != comment.Text)
            {
                comment.Text = text;
                comment.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return Ok(await ToDtos(_context.ReviewComments.Where(c => c.Id == id)).SingleAsync());
        }

        // The comment's author and the review's author may delete it.
        [HttpDelete("api/comments/{id:int}")]
        [Authorize]
        [EndpointSummary("Delete a comment the current user wrote or one on their review")]
        public async Task<IActionResult> DeleteComment(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var comment = await _context.ReviewComments
                .Where(c => c.Id == id)
                .Select(c => new { c.UserId, ReviewAuthorId = c.Review.UserId })
                .FirstOrDefaultAsync();

            if (comment == null)
            {
                return NotFound();
            }

            if (comment.UserId != userId && comment.ReviewAuthorId != userId)
            {
                return Forbid();
            }

            await _context.ReviewComments.Where(c => c.Id == id).ExecuteDeleteAsync();

            return NoContent();
        }

        private static IQueryable<ReviewCommentDto> ToDtos(IQueryable<ReviewComment> comments) =>
            comments.Select(c => new ReviewCommentDto
            {
                Id = c.Id,
                ReviewId = c.ReviewId,
                UserId = c.UserId,
                Username = c.User.UserName ?? string.Empty,
                ProfileImageUrl = c.User.ProfileImageUrl,
                Text = c.Text,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            });
    }
}
