using backend.Features;


namespace backend.Features.Ratings
{
    public class Rating
    {
        public int UserId { get; set; }

        public Users.User User { get; set; } = null!;

        public int MovieId { get; set; }

        public Movies.Movie Movie { get; set; } = null!;

        public decimal Score { get; set; }
    }
}
