using backend.Data;
using backend.Features.Analytics.DTOs;
using backend.Features.Auth;
using backend.Features.Moderation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.Analytics
{
    // Page views counted per route and day, without cookies or anything that identifies the visitor, so no
    // consent is needed. The SPA reports each page it shows; admins (see AdminAccess) read the totals.
    [ApiController]
    public class AnalyticsController : ControllerBase
    {
        public const int MaxDays = 365;

        private readonly ReelrContext _context;

        public AnalyticsController(ReelrContext context)
        {
            _context = context;
        }

        [HttpPost("api/analytics/page-views")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.PageViews)]
        [EndpointSummary("Count one view of a page. Routes the SPA does not have are ignored")]
        public async Task<IActionResult> CountPageView(PageViewDto dto)
        {
            var route = dto.Route!;
            if (!PageViewRoutes.All.Contains(route))
            {
                return NoContent();
            }

            var day = DateOnly.FromDateTime(DateTime.UtcNow);
            if (await AddViewAsync(day, route) == 0)
            {
                _context.PageViews.Add(new PageView { Day = day, Route = route, Views = 1 });
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // Another request made the day's row for this route first; count on it instead.
                    _context.ChangeTracker.Clear();
                    await AddViewAsync(day, route);
                }
            }

            return NoContent();
        }

        [HttpGet("api/admin/page-views")]
        [Authorize(Policy = AdminAccess.Policy)]
        [EndpointSummary("Get page views of the last days (default 30), per day and per route")]
        public async Task<ActionResult<PageViewStatsDto>> GetPageViews(int days = 30)
        {
            if (days is < 1 or > MaxDays)
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: $"Days must be from 1 to {MaxDays}.");
            }

            var since = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1 - days);
            var views = _context.PageViews.Where(v => v.Day >= since);

            var perDay = await views
                .GroupBy(v => v.Day)
                .Select(g => new DayViewsDto { Day = g.Key, Views = g.Sum(v => v.Views) })
                .OrderBy(d => d.Day)
                .ToListAsync();

            var perRoute = await views
                .GroupBy(v => v.Route)
                .Select(g => new RouteViewsDto { Route = g.Key, Views = g.Sum(v => v.Views) })
                .OrderByDescending(r => r.Views)
                .ThenBy(r => r.Route)
                .ToListAsync();

            return Ok(new PageViewStatsDto
            {
                Days = days,
                TotalViews = perDay.Sum(d => d.Views),
                PerDay = perDay,
                PerRoute = perRoute
            });
        }

        private Task<int> AddViewAsync(DateOnly day, string route) =>
            _context.PageViews
                .Where(v => v.Day == day && v.Route == route)
                .ExecuteUpdateAsync(set => set.SetProperty(v => v.Views, v => v.Views + 1));
    }
}
