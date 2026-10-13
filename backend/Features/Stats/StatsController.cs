using backend.Data;
using backend.Features.Auth;
using backend.Features.Diary.DTOs;
using backend.Features.Stats.DTOs;
using backend.Features.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.Stats
{
    // Statistics are made from the diary, so they follow the diary's visibility: whoever may see the diary
    // may see these, and to anyone else the stats answer like a missing user.
    [ApiController]
    public class StatsController : ControllerBase
    {
        // A day opened from the graph shows at most this many entries.
        private const int DayLimit = 50;

        private readonly ReelrContext _context;

        public StatsController(ReelrContext context)
        {
            _context = context;
        }

        [HttpGet("api/users/{userId:int}/stats")]
        [EndpointSummary("Get a member's films per day, streaks and months for a year, or the last 365 days without one")]
        public async Task<ActionResult<UserStatsDto>> GetStats(int userId, [FromQuery] int? year)
        {
            if (await _context.CanSeeDiaryAsync(User.FindUserId(), userId) != true)
            {
                return NotFound();
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            if (year is int asked && (asked < 1888 || asked > today.Year))
            {
                ModelState.AddModelError(nameof(year), $"Choose a year from 1888 to {today.Year}.");
                return ValidationProblem(ModelState);
            }

            var from = year is int start ? new DateOnly(start, 1, 1) : today.AddDays(-364);
            var to = year is int end && end < today.Year ? new DateOnly(end, 12, 31) : today;

            var after = from.ToDateTime(TimeOnly.MinValue);
            var before = to.AddDays(1).ToDateTime(TimeOnly.MinValue);

            // One member's year of diary is small, so it is grouped here rather than in SQL.
            var entries = await _context.DiaryEntries
                .Where(d => d.UserId == userId && d.WatchedAt >= after && d.WatchedAt < before)
                .Select(d => new { d.WatchedAt, d.Rating })
                .ToListAsync();

            var days = entries
                .GroupBy(e => DateOnly.FromDateTime(e.WatchedAt))
                .Select(g => new DayCountDto { Date = g.Key, Count = g.Count() })
                .OrderBy(d => d.Date)
                .ToList();

            var years = await _context.DiaryEntries
                .Where(d => d.UserId == userId)
                .Select(d => d.WatchedAt.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            return Ok(new UserStatsDto
            {
                From = from,
                To = to,
                Total = entries.Count,
                Days = days,
                LongestStreak = LongestStreak(days),
                CurrentStreak = to == today ? CurrentStreak(days, today) : null,
                BusiestWeekday = BusiestWeekday(days),
                Months = Months(from, to, entries.Select(e => (e.WatchedAt, e.Rating))),
                Years = years
            });
        }

        [HttpGet("api/users/{userId:int}/stats/days/{date}")]
        [EndpointSummary("Get the diary entries a member logged as watched on one day")]
        public async Task<ActionResult<List<DiaryEntryDto>>> GetDay(int userId, DateOnly date)
        {
            if (await _context.CanSeeDiaryAsync(User.FindUserId(), userId) != true)
            {
                return NotFound();
            }

            var after = date.ToDateTime(TimeOnly.MinValue);
            var before = date.AddDays(1).ToDateTime(TimeOnly.MinValue);

            var entries = await _context.DiaryEntries
                .Where(d => d.UserId == userId && d.WatchedAt >= after && d.WatchedAt < before)
                .OrderBy(d => d.WatchedAt)
                .ThenBy(d => d.Id)
                .Take(DayLimit)
                .Select(d => new DiaryEntryDto
                {
                    Id = d.Id,
                    TmdbId = d.Movie.TmdbId,
                    Title = d.Movie.Title,
                    PosterUrl = d.Movie.PosterUrl,
                    Rating = d.Rating,
                    Liked = d.Liked,
                    Rewatched = d.Rewatched,
                    HasReview = _context.Reviews.Any(r => r.DiaryEntryId == d.Id),
                    WatchedAt = d.WatchedAt
                })
                .ToListAsync();

            return Ok(entries);
        }

        private static int LongestStreak(List<DayCountDto> days)
        {
            var longest = 0;
            var run = 0;
            DateOnly? previous = null;

            foreach (var day in days)
            {
                run = previous is DateOnly p && day.Date == p.AddDays(1) ? run + 1 : 1;
                longest = Math.Max(longest, run);
                previous = day.Date;
            }

            return longest;
        }

        private static int CurrentStreak(List<DayCountDto> days, DateOnly today)
        {
            var logged = days.Select(d => d.Date).ToHashSet();
            // Still alive if the last film was yesterday: today may not be logged yet.
            var day = logged.Contains(today) ? today : today.AddDays(-1);
            var streak = 0;

            while (logged.Contains(day))
            {
                streak++;
                day = day.AddDays(-1);
            }

            return streak;
        }

        private static int? BusiestWeekday(List<DayCountDto> days) =>
            days.Count == 0
                ? null
                : (int)days
                    .GroupBy(d => d.Date.DayOfWeek)
                    .OrderByDescending(g => g.Sum(d => d.Count))
                    .ThenBy(g => g.Key)
                    .First().Key;

        private static List<MonthStatsDto> Months(DateOnly from, DateOnly to, IEnumerable<(DateTime WatchedAt, decimal? Rating)> entries)
        {
            var byMonth = entries.ToLookup(e => (e.WatchedAt.Year, e.WatchedAt.Month));
            var months = new List<MonthStatsDto>();

            for (var month = new DateOnly(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
            {
                var inMonth = byMonth[(month.Year, month.Month)].ToList();
                var ratings = inMonth.Where(e => e.Rating != null).Select(e => e.Rating!.Value).ToList();

                months.Add(new MonthStatsDto
                {
                    Year = month.Year,
                    Month = month.Month,
                    Count = inMonth.Count,
                    AverageRating = ratings.Count == 0 ? null : Math.Round(ratings.Average(), 2)
                });
            }

            return months;
        }
    }
}
