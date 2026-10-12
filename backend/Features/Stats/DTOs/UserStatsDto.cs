namespace backend.Features.Stats.DTOs
{
    // A member's diary as a year of days, like a contribution graph, with a summary and a month-by-month view.
    public class UserStatsDto
    {
        // The days covered, both included: a calendar year, or the 365 days up to today.
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }

        // Films logged as watched in the period. Rewatches count again.
        public int Total { get; set; }

        // Only days with at least one film, oldest first.
        public List<DayCountDto> Days { get; set; } = [];

        // The most days in a row with a film in the period.
        public int LongestStreak { get; set; }

        // Days in a row up to today, or up to yesterday if nothing is logged yet today. Null for a past year.
        public int? CurrentStreak { get; set; }

        // 0 is Sunday, as getDay() in JavaScript counts. Null when nothing was logged.
        public int? BusiestWeekday { get; set; }

        // Every month in the period, oldest first, including empty ones.
        public List<MonthStatsDto> Months { get; set; } = [];

        // Years with at least one diary entry, newest first, for choosing another year.
        public List<int> Years { get; set; } = [];
    }

    public class DayCountDto
    {
        public DateOnly Date { get; set; }
        public int Count { get; set; }
    }

    public class MonthStatsDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int Count { get; set; }

        // Of the entries with a rating, rounded to two decimals. Null when none was rated.
        public decimal? AverageRating { get; set; }
    }
}
