using backend.Data;
using backend.Features.Auth;
using backend.Features.Paging;
using backend.Features.Reviews.DTOs;
using backend.Features.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        [EndpointSummary("Get the comments on a review, oldest first, one page at a time")]
        public async Task<ActionResult<PageDto<ReviewCommentDto>>> GetComments(int reviewId, [FromQuery] PageQuery page)
        {
            if (!await _context.Reviews.AnyAsync(r => r.Id == reviewId))
            {
                return NotFound();
            }

            // Comments by people the viewer blocked are left out.
            var blocked = _context.BlockedBy(User.FindUserId());
            var comments = _context.ReviewComments.Where(c => c.ReviewId == reviewId && !blocked.Contains(c.UserId));

            if (page.After is PageCursor after)
            {
                var at = after.At;
                var id = after.Id;
                comments = comments.Where(c => c.CreatedAt > at || (c.CreatedAt == at && c.Id > id));
            }

            var rows = await ToDtos(comments)
                .OrderBy(c => c.CreatedAt)
                .ThenBy(c => c.Id)
                .Take(page.Limit + 1)
                .ToListAsync();

            return Ok(PageDto.From(rows, page.Limit, c => new PageCursor(c.CreatedAt, c.Id)));
        }

        [HttpPost("api/reviews/{reviewId:int}/comments")]
        [Authorize]
        [EndpointSummary("Comment on a review")]
        public async Task<ActionResult<ReviewCommentDto>> CreateComment(int reviewId, CreateReviewCommentDto dto)
        {
            var userId = User.GetUserId();

            var authorId = await _context.Reviews
                .Where(r => r.Id == reviewId)
                .Select(r => (int?)r.UserId)
                .FirstOrDefaultAsync();

            if (authorId == null)
            {
                return NotFound();
            }

            if (await _context.HasBlockedAsync(authorId.Value, userId))
            {
                return Problem(statusCode: StatusCodes.Status403Forbidden, title: "You can't comment on this member's reviews.");
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
            var userId = User.GetUserId();

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
            var userId = User.GetUserId();

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
