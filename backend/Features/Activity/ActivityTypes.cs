namespace backend.Features.Activity
{
    // The kinds of feed item. Clients ignore kinds they do not know, so new ones can be added safely.
    public static class ActivityTypes
    {
        public const string Watched = "watched";
        public const string Reviewed = "reviewed";
        public const string ListCreated = "listCreated";
        public const string ListAdded = "listAdded";
        public const string WatchlistAdded = "watchlistAdded";
    }
}
