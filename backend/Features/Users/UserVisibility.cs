using backend.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.Users
{
    // The single place that decides who may see which parts of a user's activity.
    public static class UserVisibility
    {
        public static bool CanSeeWatchlist(int? viewerId, int ownerId, WatchlistVisibility visibility, bool viewerFollowsOwner) =>
            CanSee(viewerId, ownerId, visibility == WatchlistVisibility.Public, visibility == WatchlistVisibility.Followers, viewerFollowsOwner);

        public static bool CanSeeDiary(int? viewerId, int ownerId, DiaryVisibility visibility, bool viewerFollowsOwner) =>
            CanSee(viewerId, ownerId, visibility == DiaryVisibility.Public, visibility == DiaryVisibility.Followers, viewerFollowsOwner);

        // The users whose diary the viewer may see, for filtering inside a query; CanSeeDiary's rule in SQL.
        public static IQueryable<int> DiaryOwnersVisibleTo(this ReelrContext context, int? viewerId)
        {
            var follows = context.Set<Follow>();

            return context.Users
                .Where(u => u.Id == viewerId
                    || u.DiaryVisibility == DiaryVisibility.Public
                    || (u.DiaryVisibility == DiaryVisibility.Followers
                        && follows.Any(f => f.FollowerId == viewerId && f.FollowedId == u.Id)))
                .Select(u => u.Id);
        }

        // Null when the owner does not exist.
        public static async Task<bool?> CanSeeWatchlistAsync(this ReelrContext context, int? viewerId, int ownerId)
        {
            var visibility = await context.Users
                .Where(u => u.Id == ownerId)
                .Select(u => (WatchlistVisibility?)u.WatchlistVisibility)
                .FirstOrDefaultAsync();

            if (visibility == null)
            {
                return null;
            }

            var follows = visibility == WatchlistVisibility.Followers && await FollowsAsync(context, viewerId, ownerId);

            return CanSeeWatchlist(viewerId, ownerId, visibility.Value, follows);
        }

        // Null when the owner does not exist.
        public static async Task<bool?> CanSeeDiaryAsync(this ReelrContext context, int? viewerId, int ownerId)
        {
            var visibility = await context.Users
                .Where(u => u.Id == ownerId)
                .Select(u => (DiaryVisibility?)u.DiaryVisibility)
                .FirstOrDefaultAsync();

            if (visibility == null)
            {
                return null;
            }

            var follows = visibility == DiaryVisibility.Followers && await FollowsAsync(context, viewerId, ownerId);

            return CanSeeDiary(viewerId, ownerId, visibility.Value, follows);
        }

        private static bool CanSee(int? viewerId, int ownerId, bool isPublic, bool followersOnly, bool viewerFollowsOwner) =>
            viewerId == ownerId || isPublic || (followersOnly && viewerFollowsOwner);

        private static async Task<bool> FollowsAsync(ReelrContext context, int? viewerId, int ownerId) =>
            viewerId != null && viewerId != ownerId
            && await context.Set<Follow>().AnyAsync(f => f.FollowerId == viewerId && f.FollowedId == ownerId);
    }
}
