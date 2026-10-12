using System.ComponentModel.DataAnnotations;

namespace backend.Features.Moderation.DTOs
{
    // Settles every open report about one item. Removed deletes the item; Dismissed leaves it.
    public class ResolveReportsDto
    {
        [Required]
        public ReportTargetType? TargetType { get; set; }

        [Required]
        public int? TargetId { get; set; }

        [Required]
        public ReportOutcome? Outcome { get; set; }
    }
}
