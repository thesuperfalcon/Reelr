using System.Text.Json.Serialization;

namespace backend.Features.Users
{
    // Who can see a user's diary (what they watched, when, and how they rated it), and their logs in the feed.
    // Reviews stay public either way; a review is something the user chose to publish, but its rating and watch
    // date come from the diary and are left out for viewers who may not see it.
    [JsonConverter(typeof(JsonStringEnumConverter<DiaryVisibility>))]
    public enum DiaryVisibility
    {
        Public = 0,
        Followers = 1,
        Private = 2
    }
}
