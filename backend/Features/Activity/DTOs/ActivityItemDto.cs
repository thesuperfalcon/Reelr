namespace backend.Features.Activity.DTOs
{
    public class ActivityItemDto
    {
        // Stable id such as "diary:123" or "listitem:4:56".
        public string Id { get; set; } = string.Empty;

        // One of ActivityTypes.
        public string Type { get; set; } = string.Empty;

        public DateTime OccurredAt { get; set; }

        public ActivityActorDto Actor { get; set; } = new();

        public ActivityMovieDto? Movie { get; set; }

        public ActivityListDto? List { get; set; }

        public ActivityReviewDto? Review { get; set; }

        // Diary details for "watched" and "reviewed".
        public decimal? Rating { get; set; }

        public bool? Liked { get; set; }

        public bool? Rewatched { get; set; }

        public DateTime? WatchedAt { get; set; }

        // More than 1 when several items by the same person were grouped; this item is the newest of them.
        public int GroupCount { get; set; } = 1;

        // The films in a group, newest first, at most a handful.
        public List<ActivityMovieDto> GroupMovies { get; set; } = [];
    }

    public class ActivityActorDto
    {
        public int Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string? ProfileImageUrl { get; set; }
    }

    public class ActivityMovieDto
    {
        public int TmdbId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? PosterUrl { get; set; }
    }

    public class ActivityListDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int MovieCount { get; set; }
    }

    public class ActivityReviewDto
    {
        public int Id { get; set; }

        // The start of the review, cut at a word.
        public string Excerpt { get; set; } = string.Empty;

        public bool IsTruncated { get; set; }

        // When true the excerpt is empty, so the feed does not spoil anything.
        public bool ContainsSpoilers { get; set; }

        public int LikeCount { get; set; }

        public int CommentCount { get; set; }
    }
}
