using backend.Features.Auth.DTOs;
using backend.Features.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace backend.Features.Auth;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly TokenService _tokenService;
    private readonly LoginThrottle _loginThrottle;

    public AuthController(
        UserManager<User> userManager, SignInManager<User> signInManager, TokenService tokenService, LoginThrottle loginThrottle)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _loginThrottle = loginThrottle;
    }

    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Register)]
    [EndpointSummary("Register a new user")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = new User
        {
            UserName = dto.Username,
            Email = dto.Email
        };

        var result = await _userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        return Ok(new { user.Id, user.UserName, user.Email });
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [EndpointSummary("Log in and receive a JWT")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

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
            return Unauthorized();
        }

        // Counts failed attempts and locks the account after too many (see AddIdentity lockout options).
        // A locked account gets the same 401 as a wrong password, so the response does not reveal it.
        var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            _loginThrottle.RecordFailure(account, address);
            return Unauthorized();
        }

        _loginThrottle.Reset(account, address);

        var token = _tokenService.CreateToken(user);

        return Ok(new { token });
    }

    // A hash made with the same hasher settings as real passwords, created once per process.
    private static string? _dummyHash;

    private string GetDummyHash() =>
        _dummyHash ??= _userManager.PasswordHasher.HashPassword(new User(), Guid.NewGuid().ToString());
}
