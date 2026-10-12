using System.Text.Json.Serialization;

namespace backend.Features.Moderation
{
    // A user's report that a review, comment or list breaks the rules. A user has at most one open report
    // per item (a filtered unique index), so repeating it does not stack up.
    public class Report
    {
        public const int NoteMaxLength = 500;

        public int Id { get; set; }

        public int ReporterId { get; set; }

        public Users.User Reporter { get; set; } = null!;

        public ReportTargetType TargetType { get; set; }

        // The id of the review, comment or list. No foreign key: the report outlives a removed item.
        public int TargetId { get; set; }

        public ReportReason Reason { get; set; }

        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; }

        // Set when an admin dealt with the item, together with what they did.
        public DateTime? ResolvedAt { get; set; }

        public int? ResolvedById { get; set; }

        public ReportOutcome? Outcome { get; set; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter<ReportTargetType>))]
    public enum ReportTargetType
    {
        Review = 0,
        Comment = 1,
        List = 2
    }

    [JsonConverter(typeof(JsonStringEnumConverter<ReportReason>))]
    public enum ReportReason
    {
        Spam = 0,
        Harassment = 1,
        // A review that gives away the plot without being marked as containing spoilers.
        UnmarkedSpoilers = 2,
        Inappropriate = 3,
        Other = 4
    }

    [JsonConverter(typeof(JsonStringEnumConverter<ReportOutcome>))]
    public enum ReportOutcome
    {
        Dismissed = 0,
        Removed = 1
    }
}
