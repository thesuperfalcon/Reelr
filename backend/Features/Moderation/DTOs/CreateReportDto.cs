using System.ComponentModel.DataAnnotations;

namespace backend.Features.Moderation.DTOs
{
    public class CreateReportDto
    {
        [Required]
        public ReportTargetType? TargetType { get; set; }

        [Required]
        public int? TargetId { get; set; }

        [Required]
        public ReportReason? Reason { get; set; }

        [MaxLength(Report.NoteMaxLength)]
        public string? Note { get; set; }
    }
}
