using backend.Data;

namespace backend.Features.Activity
{
    // Reads the logged-out visitors' community pages again just before the database goes idle (see
    // AnonymousCommunityPages), so they show the latest activity without waking the database.
    public class AnonymousCommunityRefresh : IBeforeDatabaseIdle
    {
        private readonly AnonymousCommunityPages _pages;
        private readonly IActivityFeed _feed;

        public AnonymousCommunityRefresh(AnonymousCommunityPages pages, IActivityFeed feed)
        {
            _pages = pages;
            _feed = feed;
        }

        public Task RunAsync(CancellationToken cancellationToken) => _pages.RefreshAsync(_feed);
    }
}
