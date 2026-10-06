namespace backend.Features.Activity
{
    // Bound from the "Activity" configuration section. Tuning these needs no code change.
    public class ActivityOptions
    {
        // Which IActivityFeed implementation to use. Only "Derived" exists so far.
        public string Implementation { get; set; } = "Derived";

        // How far back the community feed looks.
        public int CommunityDays { get; set; } = 30;

        // How far back the following feed looks.
        public int FollowingDays { get; set; } = 365;

        // A diary entry logged more than this many days after the watch date counts as back-dated
        // and stays out of the community feed, so back-filling an old diary does not flood it.
        public int BackdatedDays { get; set; } = 14;

        // Items of the same kind by the same person within this window are shown as one group.
        public int GroupWindowMinutes { get; set; } = 60;

        // How long the first community page is cached. 0 turns the cache off.
        public int CommunityCacheSeconds { get; set; } = 60;
    }
}
