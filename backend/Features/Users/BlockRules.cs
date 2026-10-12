using backend.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.Users
{
    // The checks behind blocking, used wherever users meet: follows, comments, likes, review lists and feeds.
    public static class BlockRules
    {
        // The users the viewer has blocked. Empty for an anonymous viewer.
        public static IQueryable<int> BlockedBy(this ReelrContext context, int? viewerId) =>
            context.Blocks.Where(b => b.BlockerId == viewerId).Select(b => b.BlockedId);

        public static Task<bool> HasBlockedAsync(this ReelrContext context, int blockerId, int blockedId) =>
            context.Blocks.AnyAsync(b => b.BlockerId == blockerId && b.BlockedId == blockedId);

        public static Task<bool> IsBlockedEitherWayAsync(this ReelrContext context, int userId, int otherId) =>
            context.Blocks.AnyAsync(b =>
                (b.BlockerId == userId && b.BlockedId == otherId) || (b.BlockerId == otherId && b.BlockedId == userId));
    }
}
