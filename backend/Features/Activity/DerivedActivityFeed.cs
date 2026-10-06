using backend.Data;
using backend.Features.Activity.DTOs;
using backend.Features.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Features.Activity
{
    // Version 1 of the feed: reads the diary, reviews, lists and watchlists directly, so deleted or
    // hidden content disappears from the feed by itself. Each page runs one small query per kind of item,
    // each bounded in size, then merges them in memory. All visibility rules for the feed live in this class.
    public class DerivedActivityFeed : IActivityFeed
    {
        // Sort order among items logged at the same instant, and part of the cursor.
        private const int WatchedRank = 1;
        private const int ReviewedRank = 2;
        private const int ListCreatedRank = 3;
        private const int ListAddedRank = 4;
        private const int WatchlistAddedRank = 5;

        // Composite keys (list + film, user + film) are packed into one number for sorting and the cursor.
        private const long KeyShift = 4294967296L;

        private const int ExcerptLength = 280;
        private const int MaxGroupMovies = 6;

        private readonly ReelrContext _context;
        private readonly ActivityOptions _options;

        public DerivedActivityFeed(ReelrContext context, IOptions<ActivityOptions> options)
        {
            _context = context;
            _options = options.Value;
        }

        public Task<ActivityPageDto> GetFollowingAsync(int viewerId, ActivityCursor? before, int limit)
        {
            var since = DateTime.UtcNow.AddDays(-_options.FollowingDays);
            var followed = _context.Set<Follow>().Where(f => f.FollowerId == viewerId).Select(f => f.FollowedId);

            // Everything the people you follow do, as long as it is public or shared with followers.
            var sources = new[]
            {
                (WatchedRank, Watched(_context.DiaryEntries.Where(d => followed.Contains(d.UserId)))),
                (ReviewedRank, Reviewed(_context.Reviews.Where(r => followed.Contains(r.UserId)))),
                (ListCreatedRank, ListsCreated(_context.MovieLists.Where(l => l.IsPublic && followed.Contains(l.UserId)))),
                (ListAddedRank, ListItemsAdded(_context.MovieListItems.Where(i => i.MovieList.IsPublic && followed.Contains(i.MovieList.UserId)))),
                // The viewer follows these users, so UserVisibility.CanSeeWatchlist comes down to "not private".
                (WatchlistAddedRank, WatchlistAdds(_context.WatchlistItems.Where(w =>
                    followed.Contains(w.UserId) && w.User.WatchlistVisibility != WatchlistVisibility.Private)))
            };

            return BuildPageAsync(sources, since, before, limit);
        }

        public Task<ActivityPageDto> GetCommunityAsync(int? viewerId, ActivityCursor? before, int limit)
        {
            var since = DateTime.UtcNow.AddDays(-_options.CommunityDays);
            var backdatedDays = _options.BackdatedDays;

            // Only what is meant for everyone: fresh logs, reviews and public lists that hold films.
            // Back-dated logs stay out so back-filling an old diary does not flood the feed.
            var sources = new[]
            {
                (WatchedRank, Watched(_context.DiaryEntries.Where(d =>
                    d.UserId != viewerId && d.LoggedAt <= d.WatchedAt.AddDays(backdatedDays)))),
                (ReviewedRank, Reviewed(_context.Reviews.Where(r => r.UserId != viewerId))),
                (ListCreatedRank, ListsCreated(_context.MovieLists.Where(l =>
                    l.IsPublic && l.UserId != viewerId && l.Items.Any())))
            };

            return BuildPageAsync(sources, since, before, limit);
        }

        // ---- Sources: one query per kind of item, all in the same shape ----

        // A log that carries a review shows up once, as "reviewed".
        private IQueryable<RawActivity> Watched(IQueryable<Diary.DiaryEntry> entries) =>
            entries
                .Where(d => !_context.Reviews.Any(r => r.DiaryEntryId == d.Id))
                .Select(d => new RawActivity
                {
                    Type = ActivityTypes.Watched,
                    Rank = WatchedRank,
                    Key = d.Id,
                    OccurredAt = d.LoggedAt,
                    ActorId = d.UserId,
                    ActorName = d.User.UserName,
                    ActorAvatar = d.User.ProfileImageUrl,
                    TmdbId = d.Movie.TmdbId,
                    Title = d.Movie.Title,
                    PosterUrl = d.Movie.PosterUrl,
                    Rating = d.Rating,
                    Liked = d.Liked,
                    Rewatched = d.Rewatched,
                    WatchedAt = d.WatchedAt
                });

        // A review dates from the log that wrote it, so a rewritten review comes back to the top.
        private static IQueryable<RawActivity> Reviewed(IQueryable<Reviews.Review> reviews) =>
            reviews.Select(r => new RawActivity
            {
                Type = ActivityTypes.Reviewed,
                Rank = ReviewedRank,
                Key = r.Id,
                OccurredAt = r.DiaryEntry != null ? r.DiaryEntry.LoggedAt : r.CreatedAt,
                ActorId = r.UserId,
                ActorName = r.User.UserName,
                ActorAvatar = r.User.ProfileImageUrl,
                TmdbId = r.Movie.TmdbId,
                Title = r.Movie.Title,
                PosterUrl = r.Movie.PosterUrl,
                ReviewId = r.Id,
                // A little more than the excerpt, so it can be cut at a word.
                ReviewText = r.Text.Substring(0, ExcerptLength + 40),
                ReviewLength = r.Text.Length,
                Rating = r.DiaryEntry != null ? r.DiaryEntry.Rating : null,
                Liked = r.DiaryEntry != null ? r.DiaryEntry.Liked : null,
                Rewatched = r.DiaryEntry != null ? r.DiaryEntry.Rewatched : null,
                WatchedAt = r.DiaryEntry != null ? r.DiaryEntry.WatchedAt : null
            });

        private static IQueryable<RawActivity> ListsCreated(IQueryable<MovieLists.MovieList> lists) =>
            lists.Select(l => new RawActivity
            {
                Type = ActivityTypes.ListCreated,
                Rank = ListCreatedRank,
                Key = l.Id,
                OccurredAt = l.CreatedAt,
                ActorId = l.UserId,
                ActorName = l.User.UserName,
                ActorAvatar = l.User.ProfileImageUrl,
                ListId = l.Id,
                ListName = l.Name,
                ListCount = l.Items.Count
            });

        private static IQueryable<RawActivity> ListItemsAdded(IQueryable<MovieLists.MovieListItem> items) =>
            items.Select(i => new RawActivity
            {
                Type = ActivityTypes.ListAdded,
                Rank = ListAddedRank,
                Key = i.MovieListId * KeyShift + i.MovieId,
                OccurredAt = i.AddedAt,
                ActorId = i.MovieList.UserId,
                ActorName = i.MovieList.User.UserName,
                ActorAvatar = i.MovieList.User.ProfileImageUrl,
                TmdbId = i.Movie.TmdbId,
                Title = i.Movie.Title,
                PosterUrl = i.Movie.PosterUrl,
                ListId = i.MovieListId,
                ListName = i.MovieList.Name,
                ListCount = i.MovieList.Items.Count
            });

        private static IQueryable<RawActivity> WatchlistAdds(IQueryable<WatchlistItems.WatchlistItem> items) =>
            items.Select(w => new RawActivity
            {
                Type = ActivityTypes.WatchlistAdded,
                Rank = WatchlistAddedRank,
                Key = w.UserId * KeyShift + w.MovieId,
                OccurredAt = w.AddedAt,
                ActorId = w.UserId,
                ActorName = w.User.UserName,
                ActorAvatar = w.User.ProfileImageUrl,
                TmdbId = w.Movie.TmdbId,
                Title = w.Movie.Title,
                PosterUrl = w.Movie.PosterUrl
            });

        // ---- Paging, merging and grouping ----

        private async Task<ActivityPageDto> BuildPageAsync(
            (int Rank, IQueryable<RawActivity> Query)[] sources, DateTime since, ActivityCursor? before, int limit)
        {
            // Grouping folds several rows into one item, so each source reads ahead a little.
            var take = Math.Min(limit * 3, 150);
            var fetched = new List<List<RawActivity>>();

            // One DbContext cannot run queries in parallel, so the sources are read one after another.
            foreach (var (rank, query) in sources)
            {
                fetched.Add(await Window(query, rank, since, before).Take(take).ToListAsync());
            }

            // A source that filled its whole read may have older rows it did not return. Rows older than its
            // oldest returned row cannot be placed yet, so they wait for the next page.
            var boundary = fetched
                .Where(rows => rows.Count == take)
                .Select(rows => rows[^1])
                .OrderByDescending(r => r.OccurredAt).ThenByDescending(r => r.Rank).ThenByDescending(r => r.Key)
                .FirstOrDefault();

            var rows = fetched
                .SelectMany(r => r)
                .Where(r => boundary == null || !new ActivityCursor(boundary.OccurredAt, boundary.Rank, boundary.Key)
                    .IsBefore(r.OccurredAt, r.Rank, r.Key))
                .OrderByDescending(r => r.OccurredAt).ThenByDescending(r => r.Rank).ThenByDescending(r => r.Key)
                .ToList();

            var items = new List<ActivityItemDto>();
            RawActivity? groupStart = null;
            RawActivity? lastUsed = null;
            var pageFull = false;

            foreach (var row in rows)
            {
                if (groupStart != null && CanGroup(groupStart, row))
                {
                    AddToGroup(items[^1], row);
                    lastUsed = row;
                    continue;
                }

                if (items.Count == limit)
                {
                    pageFull = true;
                    break;
                }

                items.Add(ToItem(row));
                groupStart = row;
                lastUsed = row;
            }

            var hasMore = pageFull || boundary != null;

            return new ActivityPageDto
            {
                Items = items,
                NextCursor = hasMore && lastUsed != null
                    ? new ActivityCursor(lastUsed.OccurredAt, lastUsed.Rank, lastUsed.Key).Encode()
                    : null
            };
        }

        // Rows inside the time window and after the cursor, newest first. The cursor test is spelled out per
        // source rank so it stays a plain comparison the database can answer from an index.
        private static IQueryable<RawActivity> Window(IQueryable<RawActivity> query, int rank, DateTime since, ActivityCursor? before)
        {
            query = query.Where(r => r.OccurredAt >= since);

            if (before is ActivityCursor cursor)
            {
                var at = cursor.OccurredAt;
                var key = cursor.Key;

                query = rank < cursor.Rank ? query.Where(r => r.OccurredAt <= at)
                    : rank > cursor.Rank ? query.Where(r => r.OccurredAt < at)
                    : query.Where(r => r.OccurredAt < at || (r.OccurredAt == at && r.Key < key));
            }

            return query.OrderByDescending(r => r.OccurredAt).ThenByDescending(r => r.Key);
        }

        // Logs, and films added to the same list, by one person within the group window become one item.
        private bool CanGroup(RawActivity groupStart, RawActivity row) =>
            row.ActorId == groupStart.ActorId
            && row.Type == groupStart.Type
            && (row.Type == ActivityTypes.Watched || (row.Type == ActivityTypes.ListAdded && row.ListId == groupStart.ListId))
            && groupStart.OccurredAt - row.OccurredAt <= TimeSpan.FromMinutes(_options.GroupWindowMinutes);

        private static void AddToGroup(ActivityItemDto item, RawActivity row)
        {
            if (item.GroupCount == 1 && item.Movie != null)
            {
                item.GroupMovies.Add(item.Movie);
            }

            item.GroupCount++;

            if (item.GroupMovies.Count < MaxGroupMovies && row.TmdbId != null)
            {
                item.GroupMovies.Add(new ActivityMovieDto { TmdbId = row.TmdbId.Value, Title = row.Title ?? string.Empty, PosterUrl = row.PosterUrl });
            }
        }

        private static ActivityItemDto ToItem(RawActivity row) => new()
        {
            Id = row.Type switch
            {
                ActivityTypes.Watched => $"diary:{row.Key}",
                ActivityTypes.Reviewed => $"review:{row.Key}",
                ActivityTypes.ListCreated => $"list:{row.Key}",
                ActivityTypes.ListAdded => $"listitem:{row.Key / KeyShift}:{row.Key % KeyShift}",
                _ => $"watchlist:{row.Key / KeyShift}:{row.Key % KeyShift}"
            },
            Type = row.Type,
            OccurredAt = DateTime.SpecifyKind(row.OccurredAt, DateTimeKind.Utc),
            Actor = new ActivityActorDto { Id = row.ActorId, UserName = row.ActorName ?? string.Empty, ProfileImageUrl = row.ActorAvatar },
            Movie = row.TmdbId == null ? null : new ActivityMovieDto { TmdbId = row.TmdbId.Value, Title = row.Title ?? string.Empty, PosterUrl = row.PosterUrl },
            List = row.ListId == null ? null : new ActivityListDto { Id = row.ListId.Value, Name = row.ListName ?? string.Empty, MovieCount = row.ListCount },
            Review = row.ReviewId == null ? null : Excerpt(row.ReviewId.Value, row.ReviewText ?? string.Empty, row.ReviewLength),
            Rating = row.Rating,
            Liked = row.Liked,
            Rewatched = row.Rewatched,
            WatchedAt = row.WatchedAt == null ? null : DateTime.SpecifyKind(row.WatchedAt.Value, DateTimeKind.Utc)
        };

        private static ActivityReviewDto Excerpt(int id, string text, int fullLength)
        {
            if (fullLength <= ExcerptLength)
            {
                return new ActivityReviewDto { Id = id, Excerpt = text };
            }

            var cut = text.LastIndexOf(' ', ExcerptLength);
            return new ActivityReviewDto
            {
                Id = id,
                Excerpt = text[..(cut > ExcerptLength / 2 ? cut : ExcerptLength)].TrimEnd(),
                IsTruncated = true
            };
        }

        // One row from any source, before it becomes a feed item.
        private sealed class RawActivity
        {
            public string Type { get; init; } = string.Empty;
            public int Rank { get; init; }
            public long Key { get; init; }
            public DateTime OccurredAt { get; init; }
            public int ActorId { get; init; }
            public string? ActorName { get; init; }
            public string? ActorAvatar { get; init; }
            public int? TmdbId { get; init; }
            public string? Title { get; init; }
            public string? PosterUrl { get; init; }
            public int? ListId { get; init; }
            public string? ListName { get; init; }
            public int ListCount { get; init; }
            public int? ReviewId { get; init; }
            public string? ReviewText { get; init; }
            public int ReviewLength { get; init; }
            public decimal? Rating { get; init; }
            public bool? Liked { get; init; }
            public bool? Rewatched { get; init; }
            public DateTime? WatchedAt { get; init; }
        }
    }
}
