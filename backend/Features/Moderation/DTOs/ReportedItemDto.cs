using backend.Features.Users.DTOs;

namespace backend.Features.Moderation.DTOs
{
    // One reported review, comment or list with its open reports, for the admin view.
    public class ReportedItemDto
    {
        public ReportTargetType TargetType { get; set; }

        public int TargetId { get; set; }

        // False when the item was deleted after it was reported; the reports can then be dismissed.
        public bool Exists { get; set; }

        public UserSummaryDto? Author { get; set; }

        // The review or comment text, or the list's name and description.
        public string? Text { get; set; }

        // The film of a review, the review a comment belongs to, or the list, for opening the item.
        public string? Context { get; set; }

        public int? ReviewId { get; set; }

        public int? ListId { get; set; }

        public int ReportCount { get; set; }

        public List<ReportReason> Reasons { get; set; } = [];

        public List<string> Notes { get; set; } = [];

        public DateTime FirstReportedAt { get; set; }
    }
}
