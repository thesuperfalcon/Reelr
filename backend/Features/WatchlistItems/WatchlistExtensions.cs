using backend.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.WatchlistItems
{
    public static class WatchlistExtensions
    {
        // Logging a film in the diary means it has been watched, so it leaves the watchlist.
        // The user can add it again afterwards; the next diary save removes it again.
        public static async Task RemoveWatchedFromWatchlistAsync(this ReelrContext context, int userId, int movieId)
        {
            var item = await context.WatchlistItems
                .FirstOrDefaultAsync(w => w.UserId == userId && w.MovieId == movieId);

            if (item != null)
            {
                context.WatchlistItems.Remove(item);
            }
        }
    }
}
