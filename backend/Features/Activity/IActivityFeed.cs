using backend.Features.Activity.DTOs;

namespace backend.Features.Activity
{
    // Builds feed pages. Controllers only use this interface, so the way the feed is computed
    // (from the source tables today, from a stored activity table later) can change behind it.
    public interface IActivityFeed
    {
        // Activity of the people the viewer follows, plus the viewer's own when includeOwn is set.
        // Types limits the page to those kinds; null means every kind.
        Task<ActivityPageDto> GetFollowingAsync(int viewerId, bool includeOwn, ActivityCursor? before, int limit, IReadOnlySet<string>? types = null);

        // Recent public activity of everyone. The viewer's own activity is left out.
        Task<ActivityPageDto> GetCommunityAsync(int? viewerId, ActivityCursor? before, int limit, IReadOnlySet<string>? types = null);

        // The films the people the viewer follows logged most recently, one entry per film, newest first.
        Task<List<FollowingFilmDto>> GetFollowingFilmsAsync(int viewerId, int limit);
    }
}
