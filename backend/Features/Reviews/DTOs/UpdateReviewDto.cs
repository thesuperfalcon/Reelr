using System.ComponentModel.DataAnnotations;

namespace backend.Features.Reviews.DTOs
{
    public class UpdateReviewDto
    {
        [Required]
        public string Text { get; set; } = null!;
    }
}
