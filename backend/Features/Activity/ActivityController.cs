using backend.Data;
using backend.Features.Activity.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace backend.Features.Activity
{
    [ApiController]
    [Route("api/feed")]
    public class ActivityController : ControllerBase
    {
        private const int DefaultLimit = 20;
        private const int MaxLimit = 50;
        private const int DefaultFilmLimit = 6;
        private const int MaxFilmLimit = 24;

        private readonly IActivityFeed _feed;
        private readonly ReelrContext _context;
        private readonly IMemoryCache _cache;
        private readonly ActivityOptions _options;

        public ActivityController(IActivityFeed feed, ReelrContext context, IMemoryCache cache, IOptions<ActivityOptions> options)
        {
            _feed = feed;
            _context = context;
            _cache = cache;
            _options = options.Value;
        }

        [Authorize]
        [HttpGet("following")]
        [EndpointSummary("Get activity from the people the current user follows, and their own, newest first")]
        public async Task<ActionResult<ActivityPageDto>> GetFollowing(
            [FromQuery] string? cursor, [FromQuery] int limit = DefaultLimit, [FromQuery] string? types = null,
            [FromQuery] bool? includeOwn = null)
        {
            if (!TryReadCursor(cursor, out var before))
            {
                return BadRequest("Invalid cursor.");
            }

            var viewerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // The user's ShowOwnActivity setting decides, unless the caller asks for one or the other,
            // e.g. "Reviews from friends" on the start page never includes the user's own reviews.
            var own = includeOwn ?? await _context.Users
                .Where(u => u.Id == viewerId)
                .Select(u => u.ShowOwnActivity)
                .FirstOrDefaultAsync();

            return Ok(await _feed.GetFollowingAsync(viewerId, own, before, Math.Clamp(limit, 1, MaxLimit), ReadTypes(types)));
        }

        [Authorize]
        [HttpGet("following/films")]
        [EndpointSummary("Get the films the people the current user follows logged most recently, one per film")]
        public async Task<ActionResult<List<FollowingFilmDto>>> GetFollowingFilms([FromQuery] int limit = DefaultFilmLimit)
        {
            var viewerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            return Ok(await _feed.GetFollowingFilmsAsync(viewerId, Math.Clamp(limit, 1, MaxFilmLimit)));
        }

        [HttpGet("community")]
        [EndpointSummary("Get recent public activity from everyone, newest first")]
        public async Task<ActionResult<ActivityPageDto>> GetCommunity(
            [FromQuery] string? cursor, [FromQuery] int limit = DefaultLimit, [FromQuery] string? types = null)
        {
            if (!TryReadCursor(cursor, out var before))
            {
                return BadRequest("Invalid cursor.");
            }

            int? viewerId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
            limit = Math.Clamp(limit, 1, MaxLimit);
            var kinds = ReadTypes(types);

            // The first page is the same for every visitor except for leaving out their own activity,
            // so it is cached briefly per viewer. Later pages are rarely requested and not cached.
            if (before != null || _options.CommunityCacheSeconds <= 0)
            {
                return Ok(await _feed.GetCommunityAsync(viewerId, before, limit, kinds));
            }

            var typesKey = kinds == null ? "all" : string.Join(',', kinds.Order());
            var page = await _cache.GetOrCreateAsync($"feed:community:{viewerId}:{limit}:{typesKey}", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.CommunityCacheSeconds);
                return _feed.GetCommunityAsync(viewerId, null, limit, kinds);
            });

            return Ok(page);
        }

        // A missing cursor means the first page; one that cannot be read is a client error.
        private static bool TryReadCursor(string? text, out ActivityCursor? cursor)
        {
            cursor = null;

            if (string.IsNullOrEmpty(text))
            {
                return true;
            }

            if (!ActivityCursor.TryDecode(text, out var decoded))
            {
                return false;
            }

            cursor = decoded;
            return true;
        }

        // A comma-separated list such as "reviewed,watched". Missing means every kind. Unknown kinds are
        // ignored, like clients ignore kinds they do not know, so a list of only unknown kinds gives an empty page.
        private static IReadOnlySet<string>? ReadTypes(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return text
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(ActivityTypes.All.Contains)
                .ToHashSet();
        }
    }
}
