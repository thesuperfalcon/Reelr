using backend.Features.Activity.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        private readonly IActivityFeed _feed;
        private readonly IMemoryCache _cache;
        private readonly ActivityOptions _options;

        public ActivityController(IActivityFeed feed, IMemoryCache cache, IOptions<ActivityOptions> options)
        {
            _feed = feed;
            _cache = cache;
            _options = options.Value;
        }

        [Authorize]
        [HttpGet("following")]
        [EndpointSummary("Get activity from the people the current user follows, newest first")]
        public async Task<ActionResult<ActivityPageDto>> GetFollowing([FromQuery] string? cursor, [FromQuery] int limit = DefaultLimit)
        {
            if (!TryReadCursor(cursor, out var before))
            {
                return BadRequest("Invalid cursor.");
            }

            var viewerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            return Ok(await _feed.GetFollowingAsync(viewerId, before, Math.Clamp(limit, 1, MaxLimit)));
        }

        [HttpGet("community")]
        [EndpointSummary("Get recent public activity from everyone, newest first")]
        public async Task<ActionResult<ActivityPageDto>> GetCommunity([FromQuery] string? cursor, [FromQuery] int limit = DefaultLimit)
        {
            if (!TryReadCursor(cursor, out var before))
            {
                return BadRequest("Invalid cursor.");
            }

            int? viewerId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
            limit = Math.Clamp(limit, 1, MaxLimit);

            // The first page is the same for every visitor except for leaving out their own activity,
            // so it is cached briefly per viewer. Later pages are rarely requested and not cached.
            if (before != null || _options.CommunityCacheSeconds <= 0)
            {
                return Ok(await _feed.GetCommunityAsync(viewerId, before, limit));
            }

            var page = await _cache.GetOrCreateAsync($"feed:community:{viewerId}:{limit}", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.CommunityCacheSeconds);
                return _feed.GetCommunityAsync(viewerId, null, limit);
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
    }
}
