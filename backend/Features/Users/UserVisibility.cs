using backend.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.Users
{
    // The single place that decides who may see which parts of a user's activity.
    public static class UserVisibility
    {
        public static bool CanSeeWatchlist(int? viewerId, int ownerId, WatchlistVisibility visibility, bool viewerFollowsOwner) =>
            viewerId == ownerId
            || visibility == WatchlistVisibility.Public
            || (visibility == WatchlistVisibility.Followers && viewerFollowsOwner);

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

            var follows = viewerId != null && viewerId != ownerId
                && visibility == WatchlistVisibility.Followers
                && await context.Set<Follow>().AnyAsync(f => f.FollowerId == viewerId && f.FollowedId == ownerId);

            return CanSeeWatchlist(viewerId, ownerId, visibility.Value, follows);
        }
    }
}
