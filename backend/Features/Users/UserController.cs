using System.ComponentModel.DataAnnotations;
using backend.Data;
using backend.Features.Auth;
using backend.Features.Paging;
using backend.Features.Users.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.Users
{
    [ApiController]
    [Route("api/users")]
    public class UserController : ControllerBase
    {
        private readonly ReelrContext _context;
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly SecurityStamps _securityStamps;
        private readonly SecurityLog _securityLog;

        public UserController(
            ReelrContext context,
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            SecurityStamps securityStamps,
            SecurityLog securityLog)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _securityStamps = securityStamps;
            _securityLog = securityLog;
        }

        [HttpGet("{id:int}")]
        [EndpointSummary("Get a user's public profile")]
        public async Task<ActionResult<UserProfileDto>> GetUser(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound();
            }

            var followerCount = await _context.Set<Follow>().CountAsync(f => f.FollowedId == id);
            var followingCount = await _context.Set<Follow>().CountAsync(f => f.FollowerId == id);

            // The endpoint is public; a valid token only adds whether the caller follows this user.
            var viewerId = User.FindUserId();
            var isFollowing = viewerId != null
                && await _context.Set<Follow>().AnyAsync(f => f.FollowerId == viewerId && f.FollowedId == id);
            var isBlocked = viewerId != null && await _context.HasBlockedAsync(viewerId.Value, id);

            return Ok(new UserProfileDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                ProfileImageUrl = user.ProfileImageUrl,
                FollowerCount = followerCount,
                FollowingCount = followingCount,
                IsFollowing = isFollowing,
                IsBlocked = isBlocked,
                WatchlistVisibility = user.WatchlistVisibility,
                CanSeeWatchlist = UserVisibility.CanSeeWatchlist(viewerId, id, user.WatchlistVisibility, isFollowing),
                DiaryVisibility = user.DiaryVisibility,
                CanSeeDiary = UserVisibility.CanSeeDiary(viewerId, id, user.DiaryVisibility, isFollowing)
            });
        }

        [HttpGet("{id:int}/avatar")]
        [EndpointSummary("Get a user's uploaded profile picture")]
        public async Task<IActionResult> GetAvatar(int id)
        {
            var avatar = await _context.UserAvatars.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == id);

            if (avatar == null)
            {
                return NotFound();
            }

            // The URL carries a version that changes with every upload, so the image can be cached for good.
            Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            Response.Headers.XContentTypeOptions = "nosniff";

            return File(avatar.Data, avatar.ContentType);
        }

        [HttpGet("search")]
        [EnableRateLimiting(RateLimitPolicies.Search)]
        [EndpointSummary("Search users by username")]
        public async Task<ActionResult<List<UserSummaryDto>>> SearchUsers([FromQuery, MaxLength(Users.User.UserNameMaxLength)] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Query must not be empty.");
            }

            // NormalizedUserName is upper case, so matching on it ignores case on every database.
            var normalized = query.Trim().ToUpperInvariant();

            var users = await _context.Users
                .Where(u => u.NormalizedUserName != null && u.NormalizedUserName.Contains(normalized))
                .OrderBy(u => u.NormalizedUserName!.StartsWith(normalized) ? 0 : 1)
                .ThenBy(u => u.UserName)
                .Take(20)
                .Select(u => new UserSummaryDto
                {
                    Id = u.Id,
                    UserName = u.UserName ?? string.Empty,
                    ProfileImageUrl = u.ProfileImageUrl
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("{id:int}/followers")]
        [EndpointSummary("Get the users who follow a user, by name, one page at a time")]
        public async Task<ActionResult<PageDto<UserSummaryDto>>> GetFollowers(int id, [FromQuery] PageQuery page)
        {
            if (!await _context.Users.AnyAsync(u => u.Id == id))
            {
                return NotFound();
            }

            var followers = _context.Set<Follow>().Where(f => f.FollowedId == id).Select(f => f.Follower);

            return Ok(await GetUserPage(followers, page));
        }

        [HttpGet("{id:int}/following")]
        [EndpointSummary("Get the users a user follows, by name, one page at a time")]
        public async Task<ActionResult<PageDto<UserSummaryDto>>> GetFollowing(int id, [FromQuery] PageQuery page)
        {
            if (!await _context.Users.AnyAsync(u => u.Id == id))
            {
                return NotFound();
            }

            var following = _context.Set<Follow>().Where(f => f.FollowerId == id).Select(f => f.Followed);

            return Ok(await GetUserPage(following, page));
        }

        // Users in name order. The id breaks ties, though names are unique apart from letter case.
        private static async Task<PageDto<UserSummaryDto>> GetUserPage(IQueryable<User> users, PageQuery page)
        {
            if (page.After is PageCursor after)
            {
                var name = after.Text;
                var id = after.Id;
                users = users.Where(u => string.Compare(u.UserName, name) > 0 || (u.UserName == name && u.Id > id));
            }

            var rows = await users
                .OrderBy(u => u.UserName)
                .ThenBy(u => u.Id)
                .Take(page.Limit + 1)
                .Select(u => new UserSummaryDto
                {
                    Id = u.Id,
                    UserName = u.UserName ?? string.Empty,
                    ProfileImageUrl = u.ProfileImageUrl
                })
                .ToListAsync();

            return PageDto.From(rows, page.Limit, u => new PageCursor(u.UserName, u.Id));
        }

        [Authorize]
        [HttpDelete("{id:int}")]
        [EndpointSummary("Delete the current user's account (requires the current password)")]
        public async Task<IActionResult> DeleteUser(int id, DeleteUserDto dto)
        {
            var userId = User.GetUserId();

            if (userId != id)
            {
                return Forbid();
            }

            var user = await _userManager.FindByIdAsync(id.ToString());

            if (user == null)
            {
                return NotFound();
            }

            // Wrong passwords count towards the same lockout as login, so this endpoint cannot be used to guess it.
            var passwordCheck = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);

            if (!passwordCheck.Succeeded)
            {
                ModelState.AddModelError(nameof(dto.Password), "The password is incorrect.");
                return ValidationProblem(ModelState);
            }

            // Follows, review likes and review comments use DeleteBehavior.Restrict (SQL Server disallows
            // two cascade paths), so they must be removed before the user.
            await using var transaction = await _context.Database.BeginTransactionAsync();

            await _context.Set<Follow>()
                .Where(f => f.FollowerId == id || f.FollowedId == id)
                .ExecuteDeleteAsync();

            await _context.Blocks
                .Where(b => b.BlockerId == id || b.BlockedId == id)
                .ExecuteDeleteAsync();

            await _context.ReviewLikes.Where(l => l.UserId == id).ExecuteDeleteAsync();
            await _context.ReviewComments.Where(c => c.UserId == id).ExecuteDeleteAsync();

            var result = await _userManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(error.Code, error.Description);
                }

                return ValidationProblem(ModelState);
            }

            await transaction.CommitAsync();
            _securityStamps.Forget(id);
            _securityLog.AccountDeleted(id);

            return NoContent();
        }

        [Authorize]
        [HttpPost("{id:int}/follow")]
        [EndpointSummary("Follow a user")]
        public async Task<IActionResult> FollowUser(int id)
        {
            var userId = User.GetUserId();

            if (userId == id)
            {
                return BadRequest("Cannot follow yourself.");
            }

            var targetExists = await _context.Users.AnyAsync(u => u.Id == id);

            if (!targetExists)
            {
                return NotFound();
            }

            if (await _context.IsBlockedEitherWayAsync(userId, id))
            {
                return Problem(statusCode: StatusCodes.Status403Forbidden, title: "You can't follow this member.");
            }

            var alreadyFollowing = await _context.Set<Follow>()
                .AnyAsync(f => f.FollowerId == userId && f.FollowedId == id);

            if (alreadyFollowing)
            {
                return Conflict("Already following this user.");
            }

            _context.Set<Follow>().Add(new Follow
            {
                FollowerId = userId,
                FollowedId = id
            });

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // Blocking twice is the same as blocking once. Follows between the two users end in both directions.
        [Authorize]
        [HttpPut("{id:int}/block")]
        [EndpointSummary("Block a user")]
        public async Task<IActionResult> BlockUser(int id)
        {
            var userId = User.GetUserId();

            if (userId == id)
            {
                return BadRequest("Cannot block yourself.");
            }

            if (!await _context.Users.AnyAsync(u => u.Id == id))
            {
                return NotFound();
            }

            if (!await _context.HasBlockedAsync(userId, id))
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                await _context.Follows
                    .Where(f => (f.FollowerId == userId && f.FollowedId == id) || (f.FollowerId == id && f.FollowedId == userId))
                    .ExecuteDeleteAsync();

                _context.Blocks.Add(new Block { BlockerId = userId, BlockedId = id, CreatedAt = DateTime.UtcNow });
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }

            return NoContent();
        }

        // Unblocking someone who is not blocked does nothing. Follows do not come back.
        [Authorize]
        [HttpDelete("{id:int}/block")]
        [EndpointSummary("Unblock a user")]
        public async Task<IActionResult> UnblockUser(int id)
        {
            var userId = User.GetUserId();

            await _context.Blocks.Where(b => b.BlockerId == userId && b.BlockedId == id).ExecuteDeleteAsync();

            return NoContent();
        }

        [Authorize]
        [HttpDelete("{id:int}/follow")]
        [EndpointSummary("Unfollow a user")]
        public async Task<IActionResult> UnfollowUser(int id)
        {
            var userId = User.GetUserId();

            var follow = await _context.Set<Follow>()
                .FirstOrDefaultAsync(f => f.FollowerId == userId && f.FollowedId == id);

            if (follow == null)
            {
                return NotFound();
            }

            _context.Set<Follow>().Remove(follow);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
