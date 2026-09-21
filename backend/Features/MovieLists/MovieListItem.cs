namespace backend.Features.MovieLists
{
    public class MovieListItem
    {
        public int MovieListId { get; set; }

        public MovieList MovieList { get; set; } = null!;

        public int MovieId { get; set; }

        public Movies.Movie Movie { get; set; } = null!;

        public DateTime AddedAt { get; set; }
    }
}
