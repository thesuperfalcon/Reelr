using backend.Data;
using backend.Features.Auth;
using backend.Features.Settings.DTOs;
using backend.Features.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace backend.Features.Settings
{
    // The current user's own settings. Everything here acts on the caller, so no route takes a user id.
    [ApiController]
    [Authorize]
    [Route("api/settings")]
    public class SettingsController : ControllerBase
    {
        private readonly ReelrContext _context;
        private readonly UserManager<User> _userManager;
        private readonly TokenService _tokenService;

        public SettingsController(ReelrContext context, UserManager<User> userManager, TokenService tokenService)
        {
            _context = context;
            _userManager = userManager;
            _tokenService = tokenService;
        }

        [HttpGet]
        [EndpointSummary("Get the current user's settings")]
        public async Task<ActionResult<UserSettingsDto>> GetSettings()
        {
            var user = await CurrentUserAsync();

            return user == null ? Unauthorized() : Ok(ToDto(user, new UserSettingsDto()));
        }

        [HttpPatch]
        [EndpointSummary("Change some of the current user's settings")]
        public async Task<ActionResult<UpdatedSettingsDto>> UpdateSettings(UpdateUserSettingsDto dto)
        {
            var user = await CurrentUserAsync();

            if (user == null)
            {
                return Unauthorized();
            }

            var result = new UpdatedSettingsDto();
            var userName = dto.UserName?.Trim();

            if (!string.IsNullOrEmpty(userName) && userName != user.UserName)
            {
                var renamed = await _userManager.SetUserNameAsync(user, userName);

                if (!renamed.Succeeded)
                {
                    return IdentityProblem(renamed);
                }

                result.Token = _tokenService.CreateToken(user);
            }

            if (dto.WatchlistVisibility is WatchlistVisibility visibility)
            {
                if (!Enum.IsDefined(visibility))
                {
                    ModelState.AddModelError(nameof(dto.WatchlistVisibility), "Unknown watchlist visibility.");
                    return ValidationProblem(ModelState);
                }

                user.WatchlistVisibility = visibility;
            }

            if (dto.ShowFriendReviews is bool show)
            {
                user.ShowFriendReviews = show;
            }

            if (dto.ShowOwnActivity is bool showOwn)
            {
                user.ShowOwnActivity = showOwn;
            }

            var updated = await _userManager.UpdateAsync(user);

            return updated.Succeeded ? Ok(ToDto(user, result)) : IdentityProblem(updated);
        }

        [HttpPost("password")]
        [EndpointSummary("Change the current user's password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
        {
            var user = await CurrentUserAsync();

            if (user == null)
            {
                return Unauthorized();
            }

            var changed = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);

            return changed.Succeeded ? NoContent() : IdentityProblem(changed);
        }

        [HttpPut("avatar")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(AvatarImage.MaxBytes + 64 * 1024)]
        [EndpointSummary("Upload a profile picture (JPEG, PNG or WebP, at most 1 MB)")]
        public async Task<ActionResult<UserSettingsDto>> UploadAvatar(IFormFile file)
        {
            var user = await CurrentUserAsync();

            if (user == null)
            {
                return Unauthorized();
            }

            if (file.Length == 0 || file.Length > AvatarImage.MaxBytes)
            {
                return BadRequest("The picture must be smaller than 1 MB.");
            }

            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer);
            var data = buffer.ToArray();
            var contentType = AvatarImage.DetectContentType(data);

            if (contentType == null)
            {
                return BadRequest("The picture must be a JPEG, PNG or WebP image.");
            }

            var avatar = await _context.Set<UserAvatar>().FindAsync(user.Id);

            if (avatar == null)
            {
                avatar = new UserAvatar { UserId = user.Id };
                _context.Set<UserAvatar>().Add(avatar);
            }

            avatar.Data = data;
            avatar.ContentType = contentType;
            avatar.UpdatedAt = DateTime.UtcNow;
            user.ProfileImageUrl = $"/api/users/{user.Id}/avatar?v={avatar.UpdatedAt.Ticks}";

            await _context.SaveChangesAsync();

            return Ok(ToDto(user, new UserSettingsDto()));
        }

        [HttpDelete("avatar")]
        [EndpointSummary("Remove the current user's profile picture")]
        public async Task<ActionResult<UserSettingsDto>> RemoveAvatar()
        {
            var user = await CurrentUserAsync();

            if (user == null)
            {
                return Unauthorized();
            }

            await _context.Set<UserAvatar>().Where(a => a.UserId == user.Id).ExecuteDeleteAsync();
            user.ProfileImageUrl = null;
            await _context.SaveChangesAsync();

            return Ok(ToDto(user, new UserSettingsDto()));
        }

        // Null when the token belongs to a user that has since been deleted.
        private async Task<User?> CurrentUserAsync()
        {
            var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        }

        private static T ToDto<T>(User user, T dto) where T : UserSettingsDto
        {
            dto.UserName = user.UserName ?? string.Empty;
            dto.Email = user.Email ?? string.Empty;
            dto.ProfileImageUrl = user.ProfileImageUrl;
            dto.WatchlistVisibility = user.WatchlistVisibility;
            dto.ShowFriendReviews = user.ShowFriendReviews;
            dto.ShowOwnActivity = user.ShowOwnActivity;
            return dto;
        }

        private ActionResult IdentityProblem(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }
    }
}
