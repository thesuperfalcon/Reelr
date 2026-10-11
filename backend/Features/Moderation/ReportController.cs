using backend.Data;
using backend.Features.Auth;
using backend.Features.Moderation.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.Moderation
{
    [ApiController]
    [Authorize]
    [Route("api/reports")]
    public class ReportController : ControllerBase
    {
        private readonly ReelrContext _context;

        public ReportController(ReelrContext context)
        {
            _context = context;
        }

        [HttpPost]
        [EndpointSummary("Report a review, comment or list that breaks the rules")]
        public async Task<IActionResult> CreateReport(CreateReportDto dto)
        {
            var userId = User.GetUserId();
            var targetType = dto.TargetType!.Value;
            var targetId = dto.TargetId!.Value;

            if (!Enum.IsDefined(targetType) || !Enum.IsDefined(dto.Reason!.Value))
            {
                ModelState.AddModelError(nameof(dto.Reason), "Unknown report type or reason.");
                return ValidationProblem(ModelState);
            }

            var authorId = await AuthorOfAsync(targetType, targetId, userId);

            if (authorId == null)
            {
                return NotFound();
            }

            if (authorId == userId)
            {
                return BadRequest("You cannot report your own content.");
            }

            // A second open report on the same item by the same user hits the unique index and becomes a 409.
            _context.Reports.Add(new Report
            {
                ReporterId = userId,
                TargetType = targetType,
                TargetId = targetId,
                Reason = dto.Reason!.Value,
                Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // The author of an item the caller can see, or null when there is no such item.
        private Task<int?> AuthorOfAsync(ReportTargetType type, int id, int viewerId) => type switch
        {
            ReportTargetType.Review => _context.Reviews.Where(r => r.Id == id).Select(r => (int?)r.UserId).FirstOrDefaultAsync(),
            ReportTargetType.Comment => _context.ReviewComments.Where(c => c.Id == id).Select(c => (int?)c.UserId).FirstOrDefaultAsync(),
            _ => _context.MovieLists
                .Where(l => l.Id == id && (l.IsPublic || l.UserId == viewerId))
                .Select(l => (int?)l.UserId)
                .FirstOrDefaultAsync()
        };
    }
}
