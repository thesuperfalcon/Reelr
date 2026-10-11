using backend.Data;
using backend.Features.Auth;
using backend.Features.Moderation;
using backend.Features.Settings.DTOs;
using backend.Features.Users;
using backend.Features.Users.DTOs;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        private readonly SignInManager<User> _signInManager;
        private readonly TokenService _tokenService;
        private readonly SecurityStamps _securityStamps;
        private readonly AdminAccess _adminAccess;
        private readonly SecurityLog _securityLog;

        public SettingsController(
            ReelrContext context,
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            TokenService tokenService,
            SecurityStamps securityStamps,
            AdminAccess adminAccess,
            SecurityLog securityLog)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _securityStamps = securityStamps;
            _adminAccess = adminAccess;
            _securityLog = securityLog;
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

                // A rename changes the security stamp, which revokes the old token; the new one carries the new stamp.
                _securityStamps.Forget(user.Id);
                result.Token = _tokenService.CreateToken(user);
                _securityLog.UsernameChanged(user.Id);
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

            if (dto.DiaryVisibility is DiaryVisibility diaryVisibility)
            {
                if (!Enum.IsDefined(diaryVisibility))
                {
                    ModelState.AddModelError(nameof(dto.DiaryVisibility), "Unknown diary visibility.");
                    return ValidationProblem(ModelState);
                }

                user.DiaryVisibility = diaryVisibility;
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
        [EndpointSummary("Change the current user's password. Signs out every other device and returns a new token for this one")]
        public async Task<ActionResult<TokenDto>> ChangePassword(ChangePasswordDto dto)
        {
            var user = await CurrentUserAsync();

            if (user == null)
            {
                return Unauthorized();
            }

            // Wrong current passwords count towards the same lockout as login. Without this, a stolen token
            // could be used to guess the password here without limit and then take over the account.
            var passwordCheck = await _signInManager.CheckPasswordSignInAsync(user, dto.CurrentPassword, lockoutOnFailure: true);

            if (!passwordCheck.Succeeded)
            {
                _securityLog.PasswordChangeRefused(user.Id);
                ModelState.AddModelError(nameof(dto.CurrentPassword), "The current password is incorrect.");
                return ValidationProblem(ModelState);
            }

            var changed = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);

            if (!changed.Succeeded)
            {
                return IdentityProblem(changed);
            }

            // The change revoked every token, including the one used here, so this device gets a new one.
            _securityStamps.Forget(user.Id);
            _securityLog.PasswordChanged(user.Id);
            return Ok(new TokenDto { Token = _tokenService.CreateToken(user) });
        }

        [HttpGet("export")]
        [EndpointSummary("Download everything Reelr stores about the current user, as a JSON file")]
        public async Task<IActionResult> Export()
        {
            var user = await CurrentUserAsync();

            if (user == null)
            {
                return Unauthorized();
            }

            var export = await DataExport.BuildAsync(_context, user);
            var json = JsonSerializer.SerializeToUtf8Bytes(export, ExportJsonOptions);

            return File(json, "application/json", $"reelr-{user.UserName}-{DateTime.UtcNow:yyyy-MM-dd}.json");
        }

        [HttpGet("blocked")]
        [EndpointSummary("Get the users the current user has blocked")]
        public async Task<ActionResult<List<UserSummaryDto>>> GetBlocked()
        {
            var userId = User.GetUserId();

            var blocked = await _context.Blocks
                .Where(b => b.BlockerId == userId)
                .OrderBy(b => b.Blocked.UserName)
                .Select(b => new UserSummaryDto
                {
                    Id = b.Blocked.Id,
                    UserName = b.Blocked.UserName ?? string.Empty,
                    ProfileImageUrl = b.Blocked.ProfileImageUrl
                })
                .ToListAsync();

            return Ok(blocked);
        }

        [HttpPost("logout-everywhere")]
        [EndpointSummary("Sign out on every device, including this one")]
        public async Task<IActionResult> LogoutEverywhere()
        {
            var user = await CurrentUserAsync();

            if (user == null)
            {
                return Unauthorized();
            }

            await _userManager.UpdateSecurityStampAsync(user);
            _securityStamps.Forget(user.Id);
            _securityLog.SignedOutEverywhere(user.Id);

            return NoContent();
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
            var upload = buffer.ToArray();

            if (AvatarImage.DetectContentType(upload) == null)
            {
                return BadRequest("The picture must be a JPEG, PNG or WebP image.");
            }

            // Stored as a newly drawn picture, so metadata such as the GPS position from a phone camera is gone.
            var data = AvatarImage.Normalize(upload);

            if (data == null)
            {
                return BadRequest("The picture could not be read. Try another one.");
            }

            var avatar = await _context.Set<UserAvatar>().FindAsync(user.Id);

            if (avatar == null)
            {
                avatar = new UserAvatar { UserId = user.Id };
                _context.Set<UserAvatar>().Add(avatar);
            }

            avatar.Data = data;
            avatar.ContentType = AvatarImage.StoredContentType;
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

        private static readonly JsonSerializerOptions ExportJsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

        // Null when the token belongs to a user that has since been deleted.
        private async Task<User?> CurrentUserAsync()
        {
            var id = User.GetUserId();
            return await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        }

        private T ToDto<T>(User user, T dto) where T : UserSettingsDto
        {
            dto.IsAdmin = _adminAccess.IsAdmin(user.Id);
            dto.UserName = user.UserName ?? string.Empty;
            dto.Email = user.Email ?? string.Empty;
            dto.ProfileImageUrl = user.ProfileImageUrl;
            dto.WatchlistVisibility = user.WatchlistVisibility;
            dto.DiaryVisibility = user.DiaryVisibility;
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
