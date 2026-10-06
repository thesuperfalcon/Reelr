using System.ComponentModel.DataAnnotations;

namespace backend.Features.Reviews.DTOs
{
    public class UpdateReviewCommentDto
    {
        [Required]
        [MaxLength(ReviewComment.MaxLength)]
        public string Text { get; set; } = null!;
    }
}
