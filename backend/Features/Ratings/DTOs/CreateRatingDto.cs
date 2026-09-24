using System.ComponentModel.DataAnnotations;

namespace backend.Features.Ratings.DTOs
{
    public class CreateRatingDto
    {
        [Range(typeof(decimal), "0", "5")]
        public decimal Score { get; set; }
    }
}
