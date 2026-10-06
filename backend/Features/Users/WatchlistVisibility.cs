using System.Text.Json.Serialization;

namespace backend.Features.Users
{
    // Who can see a user's watchlist, and their watchlist activity in the feed.
    [JsonConverter(typeof(JsonStringEnumConverter<WatchlistVisibility>))]
    public enum WatchlistVisibility
    {
        Public = 0,
        Followers = 1,
        Private = 2
    }
}
