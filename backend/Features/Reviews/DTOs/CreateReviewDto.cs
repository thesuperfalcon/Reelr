using System.ComponentModel.DataAnnotations;

namespace backend.Features.Reviews.DTOs
{
    public class CreateReviewDto
    {
        [Required]
        [MaxLength(Review.MaxLength)]
        public string Text { get; set; } = null!;
    }
}
