using backend.Data;
using backend.Features.Auth;
using backend.Features.Moderation.DTOs;
using backend.Features.Users.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.Moderation
{
    // Moderation for admins (see AdminAccess): the reported items and what to do about them.
    [ApiController]
    [Authorize(Policy = AdminAccess.Policy)]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private const int TextPreviewLength = 600;

        private readonly ReelrContext _context;
        private readonly SecurityLog _securityLog;

        public AdminController(ReelrContext context, SecurityLog securityLog)
        {
            _context = context;
            _securityLog = securityLog;
        }

        [HttpGet("reports")]
        [EndpointSummary("Get the items with open reports, the most reported first")]
        public async Task<ActionResult<List<ReportedItemDto>>> GetOpenReports()
        {
            var open = await _context.Reports
                .Where(r => r.ResolvedAt == null)
                .Select(r => new { r.TargetType, r.TargetId, r.Reason, r.Note, r.CreatedAt })
                .ToListAsync();

            var items = open
                .GroupBy(r => (r.TargetType, r.TargetId))
                .Select(g => new ReportedItemDto
                {
                    TargetType = g.Key.TargetType,
                    TargetId = g.Key.TargetId,
                    ReportCount = g.Count(),
                    Reasons = g.Select(r => r.Reason).Distinct().ToList(),
                    Notes = g.Where(r => r.Note != null).Select(r => r.Note!).ToList(),
                    FirstReportedAt = g.Min(r => r.CreatedAt)
                })
                .OrderByDescending(i => i.ReportCount)
                .ThenBy(i => i.FirstReportedAt)
                .ToList();

            await FillReviewsAsync(items.Where(i => i.TargetType == ReportTargetType.Review).ToList());
            await FillCommentsAsync(items.Where(i => i.TargetType == ReportTargetType.Comment).ToList());
            await FillListsAsync(items.Where(i => i.TargetType == ReportTargetType.List).ToList());

            return Ok(items);
        }

        [HttpPost("reports/resolve")]
        [EndpointSummary("Settle every open report about an item: remove the item, or dismiss the reports")]
        public async Task<IActionResult> Resolve(ResolveReportsDto dto)
        {
            var type = dto.TargetType!.Value;
            var id = dto.TargetId!.Value;
            var outcome = dto.Outcome!.Value;

            if (!Enum.IsDefined(type) || !Enum.IsDefined(outcome))
            {
                return BadRequest("Unknown report type or outcome.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var resolved = await _context.Reports
                .Where(r => r.ResolvedAt == null && r.TargetType == type && r.TargetId == id)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(r => r.ResolvedAt, DateTime.UtcNow)
                    .SetProperty(r => r.ResolvedById, User.GetUserId())
                    .SetProperty(r => r.Outcome, outcome));

            if (resolved == 0)
            {
                return NotFound();
            }

            if (outcome == ReportOutcome.Removed)
            {
                // Likes and comments go with a review, and films go with a list, through cascading deletes.
                _ = type switch
                {
                    ReportTargetType.Review => await _context.Reviews.Where(r => r.Id == id).ExecuteDeleteAsync(),
                    ReportTargetType.Comment => await _context.ReviewComments.Where(c => c.Id == id).ExecuteDeleteAsync(),
                    _ => await _context.MovieLists.Where(l => l.Id == id).ExecuteDeleteAsync()
                };
            }

            await transaction.CommitAsync();
            _securityLog.ReportsResolved(User.GetUserId(), type.ToString(), id, outcome.ToString());

            return NoContent();
        }

        private async Task FillReviewsAsync(List<ReportedItemDto> items)
        {
            var ids = items.Select(i => i.TargetId).ToList();
            var reviews = await _context.Reviews
                .Where(r => ids.Contains(r.Id))
                .Select(r => new { r.Id, r.UserId, r.User.UserName, r.User.ProfileImageUrl, r.Text, r.Movie.Title })
                .ToDictionaryAsync(r => r.Id);

            foreach (var item in items)
            {
                if (reviews.TryGetValue(item.TargetId, out var review))
                {
                    item.Exists = true;
                    item.Author = new UserSummaryDto { Id = review.UserId, UserName = review.UserName ?? string.Empty, ProfileImageUrl = review.ProfileImageUrl };
                    item.Text = Preview(review.Text);
                    item.Context = review.Title;
                    item.ReviewId = review.Id;
                }
            }
        }

        private async Task FillCommentsAsync(List<ReportedItemDto> items)
        {
            var ids = items.Select(i => i.TargetId).ToList();
            var comments = await _context.ReviewComments
                .Where(c => ids.Contains(c.Id))
                .Select(c => new { c.Id, c.UserId, c.User.UserName, c.User.ProfileImageUrl, c.Text, c.ReviewId, c.Review.Movie.Title })
                .ToDictionaryAsync(c => c.Id);

            foreach (var item in items)
            {
                if (comments.TryGetValue(item.TargetId, out var comment))
                {
                    item.Exists = true;
                    item.Author = new UserSummaryDto { Id = comment.UserId, UserName = comment.UserName ?? string.Empty, ProfileImageUrl = comment.ProfileImageUrl };
                    item.Text = Preview(comment.Text);
                    item.Context = $"Comment on a review of {comment.Title}";
                    item.ReviewId = comment.ReviewId;
                }
            }
        }

        private async Task FillListsAsync(List<ReportedItemDto> items)
        {
            var ids = items.Select(i => i.TargetId).ToList();
            var lists = await _context.MovieLists
                .Where(l => ids.Contains(l.Id))
                .Select(l => new { l.Id, l.UserId, l.User.UserName, l.User.ProfileImageUrl, l.Name, l.Description })
                .ToDictionaryAsync(l => l.Id);

            foreach (var item in items)
            {
                if (lists.TryGetValue(item.TargetId, out var list))
                {
                    item.Exists = true;
                    item.Author = new UserSummaryDto { Id = list.UserId, UserName = list.UserName ?? string.Empty, ProfileImageUrl = list.ProfileImageUrl };
                    item.Text = Preview(string.IsNullOrEmpty(list.Description) ? list.Name : $"{list.Name}\n\n{list.Description}");
                    item.Context = "List";
                    item.ListId = list.Id;
                }
            }
        }

        private static string Preview(string text) =>
            text.Length <= TextPreviewLength ? text : text[..TextPreviewLength] + "…";
    }
}
