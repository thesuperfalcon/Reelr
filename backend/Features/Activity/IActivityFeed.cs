using backend.Features.Activity.DTOs;

namespace backend.Features.Activity
{
    // Builds feed pages. Controllers only use this interface, so the way the feed is computed
    // (from the source tables today, from a stored activity table later) can change behind it.
    public interface IActivityFeed
    {
        // Activity of the people the viewer follows.
        Task<ActivityPageDto> GetFollowingAsync(int viewerId, ActivityCursor? before, int limit);

        // Recent public activity of everyone. The viewer's own activity is left out.
        Task<ActivityPageDto> GetCommunityAsync(int? viewerId, ActivityCursor? before, int limit);
    }
}
