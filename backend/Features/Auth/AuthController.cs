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

    public AuthController(UserManager<User> userManager, SignInManager<User> signInManager, TokenService tokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
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

        if (user == null)
        {
            // Hash the password anyway, so an unknown user takes as long to answer as a wrong password.
            // Otherwise the response time would tell which email addresses have an account.
            _userManager.PasswordHasher.VerifyHashedPassword(new User(), GetDummyHash(), dto.Password);
            return Unauthorized();
        }

        // Counts failed attempts and locks the account after too many (see AddIdentity lockout options).
        // A locked account gets the same 401 as a wrong password, so the response does not reveal it.
        var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            return Unauthorized();
        }

        var token = _tokenService.CreateToken(user);

        return Ok(new { token });
    }

    // A hash made with the same hasher settings as real passwords, created once per process.
    private static string? _dummyHash;

    private string GetDummyHash() =>
        _dummyHash ??= _userManager.PasswordHasher.HashPassword(new User(), Guid.NewGuid().ToString());
}
