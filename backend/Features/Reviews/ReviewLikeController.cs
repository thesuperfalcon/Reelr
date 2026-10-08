using backend.Data;
using backend.Features.Auth;
using backend.Features.Reviews.DTOs;
using backend.Features.Users.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.Reviews
{
    [ApiController]
    [Route("api/reviews/{reviewId:int}")]
    public class ReviewLikeController : ControllerBase
    {
        private readonly ReelrContext _context;

        public ReviewLikeController(ReelrContext context)
        {
            _context = context;
        }

        [HttpGet("likes")]
        [EndpointSummary("Get the users who like a review, newest like first")]
        public async Task<ActionResult<List<UserSummaryDto>>> GetLikes(int reviewId)
        {
            if (!await _context.Reviews.AnyAsync(r => r.Id == reviewId))
            {
                return NotFound();
            }

            var users = await _context.ReviewLikes
                .Where(l => l.ReviewId == reviewId)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new UserSummaryDto
                {
                    Id = l.User.Id,
                    UserName = l.User.UserName ?? string.Empty,
                    ProfileImageUrl = l.User.ProfileImageUrl
                })
                .ToListAsync();

            return Ok(users);
        }

        // Liking twice is the same as liking once.
        [HttpPut("like")]
        [Authorize]
        [EndpointSummary("Like a review")]
        public async Task<ActionResult<ReviewLikeStateDto>> Like(int reviewId)
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

            if (authorId == userId)
            {
                return BadRequest("You cannot like your own review.");
            }

            if (!await _context.ReviewLikes.AnyAsync(l => l.ReviewId == reviewId && l.UserId == userId))
            {
                _context.ReviewLikes.Add(new ReviewLike { ReviewId = reviewId, UserId = userId, CreatedAt = DateTime.UtcNow });

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // A parallel request may have liked it first, which has the same outcome.
                    if (!await _context.ReviewLikes.AsNoTracking().AnyAsync(l => l.ReviewId == reviewId && l.UserId == userId))
                    {
                        throw;
                    }
                }
            }

            return Ok(await StateAsync(reviewId, true));
        }

        // Unliking a review that is not liked does nothing.
        [HttpDelete("like")]
        [Authorize]
        [EndpointSummary("Remove a like from a review")]
        public async Task<ActionResult<ReviewLikeStateDto>> Unlike(int reviewId)
        {
            var userId = User.GetUserId();

            if (!await _context.Reviews.AnyAsync(r => r.Id == reviewId))
            {
                return NotFound();
            }

            await _context.ReviewLikes
                .Where(l => l.ReviewId == reviewId && l.UserId == userId)
                .ExecuteDeleteAsync();

            return Ok(await StateAsync(reviewId, false));
        }

        private async Task<ReviewLikeStateDto> StateAsync(int reviewId, bool likedByMe) => new()
        {
            LikeCount = await _context.ReviewLikes.CountAsync(l => l.ReviewId == reviewId),
            LikedByMe = likedByMe
        };
    }
}
