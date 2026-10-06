using System.ComponentModel.DataAnnotations;

namespace backend.Features.Reviews.DTOs
{
    // Fields left null stay as they are, so the spoiler flag can change without touching the text.
    public class UpdateReviewDto
    {
        [MinLength(1)]
        [MaxLength(Review.MaxLength)]
        public string? Text { get; set; }

        public bool? ContainsSpoilers { get; set; }
    }
}
