namespace backend.Features.Ratings.DTOs
{
    public class UpdateRatingDto
    {
        [HalfStars]
        public decimal Score { get; set; }
    }
}
