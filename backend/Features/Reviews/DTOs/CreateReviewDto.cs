using System.ComponentModel.DataAnnotations;

namespace backend.Features.Reviews.DTOs
{
    public class CreateReviewDto
    {
        [Required]
        public string Text { get; set; } = null!;
    }
}
