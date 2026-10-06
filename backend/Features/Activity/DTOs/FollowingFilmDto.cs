namespace backend.Features.Activity.DTOs
{
    // One film on the "New from friends" shelf, with the people you follow who logged it, newest first.
    public class FollowingFilmDto
    {
        public ActivityMovieDto Movie { get; set; } = new();

        // When the newest of these logs was saved.
        public DateTime LastLoggedAt { get; set; }

        // How many of the people you follow logged the film. Watchers holds only the newest few.
        public int WatcherCount { get; set; }

        public List<FollowingFilmWatcherDto> Watchers { get; set; } = [];
    }

    public class FollowingFilmWatcherDto
    {
        public ActivityActorDto Actor { get; set; } = new();

        // From the person's newest log of the film.
        public decimal? Rating { get; set; }

        public bool? Liked { get; set; }
    }
}
