using backend.Data;
using backend.Features.Users.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace backend.Features.Users
{
    [ApiController]
    [Route("api/users")]
    public class UserController : ControllerBase
    {
        private readonly ReelrContext _context;
        private readonly UserManager<User> _userManager;

        public UserController(ReelrContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
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

            return Ok(new UserProfileDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                ProfileImageUrl = user.ProfileImageUrl,
                FollowerCount = followerCount,
                FollowingCount = followingCount
            });
        }

        [Authorize]
        [HttpPut("{id:int}")]
        [EndpointSummary("Update the current user's profile")]
        public async Task<ActionResult<UserProfileDto>> UpdateUser(int id, UpdateUserDto dto)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (userId != id)
            {
                return Forbid();
            }

            var user = await _userManager.FindByIdAsync(id.ToString());

            if (user == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrWhiteSpace(dto.Username) && dto.Username != user.UserName)
            {
                var setNameResult = await _userManager.SetUserNameAsync(user, dto.Username);

                if (!setNameResult.Succeeded)
                {
                    foreach (var error in setNameResult.Errors)
                    {
                        ModelState.AddModelError(error.Code, error.Description);
                    }

                    return ValidationProblem(ModelState);
                }
            }

            if (dto.ProfileImageUrl != null)
            {
                user.ProfileImageUrl = dto.ProfileImageUrl;
                await _userManager.UpdateAsync(user);
            }

            var followerCount = await _context.Set<Follow>().CountAsync(f => f.FollowedId == id);
            var followingCount = await _context.Set<Follow>().CountAsync(f => f.FollowerId == id);

            return Ok(new UserProfileDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                ProfileImageUrl = user.ProfileImageUrl,
                FollowerCount = followerCount,
                FollowingCount = followingCount
            });
        }

        [Authorize]
        [HttpDelete("{id:int}")]
        [EndpointSummary("Delete the current user's account")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (userId != id)
            {
                return Forbid();
            }

            var user = await _userManager.FindByIdAsync(id.ToString());

            if (user == null)
            {
                return NotFound();
            }

            await _userManager.DeleteAsync(user);

            return NoContent();
        }

        [Authorize]
        [HttpPost("{id:int}/follow")]
        [EndpointSummary("Follow a user")]
        public async Task<IActionResult> FollowUser(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (userId == id)
            {
                return BadRequest("Cannot follow yourself.");
            }

            var targetExists = await _context.Users.AnyAsync(u => u.Id == id);

            if (!targetExists)
            {
                return NotFound();
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

        [Authorize]
        [HttpDelete("{id:int}/follow")]
        [EndpointSummary("Unfollow a user")]
        public async Task<IActionResult> UnfollowUser(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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
