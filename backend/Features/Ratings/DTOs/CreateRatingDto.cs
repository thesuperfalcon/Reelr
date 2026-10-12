namespace backend.Features.Ratings.DTOs
{
    public class CreateRatingDto
    {
        [HalfStars]
        public decimal Score { get; set; }
    }
}
