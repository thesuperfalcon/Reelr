using backend.Features.Auth.DTOs;
using backend.Features.Invites;
using backend.Features.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace backend.Features.Auth;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    // The same answer for every email form, whether or not the address has an account.
    private const string CheckYourEmail = "If the address can receive email, a message is on its way. Check your inbox.";

    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly TokenService _tokenService;
    private readonly LoginThrottle _loginThrottle;
    private readonly AccountEmails _accountEmails;
    private readonly SecurityStamps _securityStamps;
    private readonly SecurityLog _securityLog;
    private readonly RefreshTokens _refreshTokens;
    private readonly InviteCodes _inviteCodes;
    private readonly DemoAccounts _demoAccounts;

    public AuthController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        TokenService tokenService,
        LoginThrottle loginThrottle,
        AccountEmails accountEmails,
        SecurityStamps securityStamps,
        SecurityLog securityLog,
        RefreshTokens refreshTokens,
        InviteCodes inviteCodes,
        DemoAccounts demoAccounts)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _loginThrottle = loginThrottle;
        _accountEmails = accountEmails;
        _securityStamps = securityStamps;
        _securityLog = securityLog;
        _refreshTokens = refreshTokens;
        _inviteCodes = inviteCodes;
        _demoAccounts = demoAccounts;
    }

    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Register)]
    [EndpointSummary("Register a new user. The account can log in once its email address is confirmed")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var user = new User
        {
            UserName = dto.Username,
            Email = dto.Email,
            TermsAcceptedAt = DateTime.UtcNow
        };

        // Password rules are checked first: Identity stops at a taken email before it looks at the password,
        // so otherwise a weak password would get a different answer depending on whether the email is taken.
        var passwordErrors = new List<IdentityError>();

        foreach (var validator in _userManager.PasswordValidators)
        {
            passwordErrors.AddRange((await validator.ValidateAsync(_userManager, user, dto.Password)).Errors);
        }

        if (passwordErrors.Count > 0)
        {
            return IdentityProblem(passwordErrors);
        }

        // While registration is invite only, the invite is claimed and the account created together: if the
        // account is not created (taken name or email), the transaction is rolled back and the code still works.
        await using var transaction = _inviteCodes.Required ? await _inviteCodes.BeginTransactionAsync() : null;
        int? inviteId = null;

        if (_inviteCodes.Required)
        {
            inviteId = await _inviteCodes.ClaimAsync(dto.InviteCode);

            if (inviteId == null)
            {
                _securityLog.InviteRefused();
                ModelState.AddModelError(nameof(dto.InviteCode), "This invite code is not valid or has already been used.");
                return ValidationProblem(ModelState);
            }
        }

        var result = await _userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
        {
            // Usernames are public, so a taken one may be reported. A taken email may not: that answer goes to
            // the address's owner by email instead, and the form answers exactly as for a new account.
            var otherErrors = result.Errors
                .Where(e => e.Code != nameof(IdentityErrorDescriber.DuplicateEmail))
                .ToList();

            if (otherErrors.Count > 0)
            {
                return IdentityProblem(otherErrors);
            }

            // A new account hashes the password; doing the same here keeps the response time alike.
            _userManager.PasswordHasher.HashPassword(user, dto.Password);
            _accountEmails.SendAlreadyRegistered(dto.Email);
            _securityLog.RegisteredWithTakenEmail();

            return Accepted(new { message = CheckYourEmail });
        }

        if (transaction != null)
        {
            await _inviteCodes.SetUserAsync(inviteId!.Value, user.Id);
            await transaction.CommitAsync();
        }

        await _accountEmails.SendConfirmationAsync(user);
        _securityLog.Registered(user.Id);

        return Accepted(new { message = CheckYourEmail });
    }

    [HttpPost("confirm-email")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [EndpointSummary("Confirm an email address with the token from the confirmation email")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailDto dto)
    {
        var user = await _userManager.FindByIdAsync(dto.UserId!.Value.ToString());

        if (user == null)
        {
            return InvalidLink();
        }

        if (user.EmailConfirmed)
        {
            return NoContent();
        }

        var result = await _userManager.ConfirmEmailAsync(user, dto.Token);

        if (!result.Succeeded)
        {
            return InvalidLink();
        }

        _securityLog.EmailConfirmed(user.Id);
        return NoContent();
    }

    [HttpPost("resend-confirmation")]
    [EnableRateLimiting(RateLimitPolicies.AccountEmail)]
    [EndpointSummary("Send the confirmation email again")]
    public async Task<IActionResult> ResendConfirmation([FromBody] EmailDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);

        if (user is { EmailConfirmed: false })
        {
            await _accountEmails.SendConfirmationAsync(user);
        }

        return Accepted(new { message = CheckYourEmail });
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting(RateLimitPolicies.AccountEmail)]
    [EndpointSummary("Email a link for choosing a new password")]
    public async Task<IActionResult> ForgotPassword([FromBody] EmailDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);

        // A demo account's password stays as it is (see DemoAccounts); the answer is the same as for anyone.
        if (user != null && !_demoAccounts.IsDemo(user.Id))
        {
            await _accountEmails.SendPasswordResetAsync(user);
            _securityLog.PasswordResetRequested(user.Id);
        }

        return Accepted(new { message = CheckYourEmail });
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [EndpointSummary("Choose a new password with the token from the reset email")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        var user = await _userManager.FindByIdAsync(dto.UserId!.Value.ToString());

        if (user == null || _demoAccounts.IsDemo(user.Id))
        {
            return InvalidLink();
        }

        var result = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);

        if (!result.Succeeded)
        {
            return result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))
                ? InvalidLink()
                : IdentityProblem(result.Errors);
        }

        // The reset changed the security stamp, which signs out every device. The link came by email,
        // so it also proves the address, and it ends a lockout the forgotten password may have caused.
        _securityStamps.Forget(user.Id);
        user.EmailConfirmed = true;
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        await _userManager.UpdateAsync(user);
        _securityLog.PasswordReset(user.Id);

        return NoContent();
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [EndpointSummary("Log in. Returns a short access token and sets the refresh cookie")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.UserInput);

        if (user == null)
        {
            user = await _userManager.FindByNameAsync(dto.UserInput);
        }

        // Unknown names are throttled too, so a 429 does not tell whether an account exists.
        var account = user != null ? $"id:{user.Id}" : $"name:{dto.UserInput.Trim().ToUpperInvariant()}";
        var address = HttpContext.Connection.RemoteIpAddress;

        if (_loginThrottle.IsBlocked(account, address))
        {
            _securityLog.LoginFailed(user?.Id, LoginFailure.Throttled);
            Response.Headers.RetryAfter = ((int)_loginThrottle.Window.TotalSeconds).ToString();
            return Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Too many failed logins for this account from this address. Try again later.");
        }

        if (user == null)
        {
            // Hash the password anyway, so an unknown user takes as long to answer as a wrong password.
            // Otherwise the response time would tell which email addresses have an account.
            _userManager.PasswordHasher.VerifyHashedPassword(new User(), GetDummyHash(), dto.Password);
            _loginThrottle.RecordFailure(account, address);
            _securityLog.LoginFailed(null, LoginFailure.UnknownAccount);
            return Unauthorized();
        }

        // Counts failed attempts and locks the account after too many (see AddIdentity lockout options).
        // A locked account gets the same 401 as a wrong password, so the response does not reveal it.
        var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            _loginThrottle.RecordFailure(account, address);
            _securityLog.LoginFailed(user.Id, result.IsLockedOut ? LoginFailure.LockedOut : LoginFailure.WrongPassword);
            return Unauthorized();
        }

        _loginThrottle.Reset(account, address);

        // Checked only after the right password, so it does not reveal unconfirmed accounts to anyone else.
        if (!user.EmailConfirmed)
        {
            _securityLog.LoginFailed(user.Id, LoginFailure.EmailNotConfirmed);
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Confirm your email address first. The link is in the email we sent when you signed up.",
                type: "email-not-confirmed");
        }

        var token = _tokenService.CreateToken(user);
        await _refreshTokens.IssueAsync(Response, user);
        _securityLog.LoginSucceeded(user.Id);

        return Ok(new { token });
    }

    // Not under the login limit: every open tab refreshes now and then. The general write limit applies.
    [HttpPost("refresh")]
    [RequireCsrfHeader]
    [EndpointSummary("Exchange the refresh cookie for a new access token and a new refresh cookie. 401 when signed out")]
    public async Task<IActionResult> Refresh()
    {
        var user = await _refreshTokens.RotateAsync(Request, Response);

        if (user == null)
        {
            return Unauthorized();
        }

        return Ok(new { token = _tokenService.CreateToken(user) });
    }

    [HttpPost("logout")]
    [RequireCsrfHeader]
    [EndpointSummary("End this device's sign-in and clear the refresh cookie")]
    public async Task<IActionResult> Logout()
    {
        if (await _refreshTokens.RevokeAsync(Request, Response) is int userId)
        {
            _securityLog.LoggedOut(userId);
        }

        return NoContent();
    }

    private ObjectResult InvalidLink() =>
        Problem(statusCode: StatusCodes.Status400BadRequest, title: "This link is invalid or has expired. Ask for a new one.");

    private ActionResult IdentityProblem(IEnumerable<IdentityError> errors)
    {
        foreach (var error in errors)
        {
            ModelState.AddModelError(error.Code, error.Description);
        }

        return ValidationProblem(ModelState);
    }

    // A hash made with the same hasher settings as real passwords, created once per process.
    private static string? _dummyHash;

    private string GetDummyHash() =>
        _dummyHash ??= _userManager.PasswordHasher.HashPassword(new User(), Guid.NewGuid().ToString());
}
