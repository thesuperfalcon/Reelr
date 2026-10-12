using backend.Features.Email;
using backend.Features.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace backend.Features.Auth;

/// <summary>
/// The emails about a user's account: confirming the address, resetting the password, and telling the owner
/// of an address that someone tried to register with it. Links point to the frontend ("App:BaseUrl") and carry
/// the user id and token after "#", so they never reach a server log or another site's Referer header.
/// At most a few emails go to one address per hour, so the forms cannot be used to flood someone's inbox.
/// </summary>
public class AccountEmails
{
    private const int MaxPerAddressPerHour = 3;

    private readonly UserManager<User> _userManager;
    private readonly EmailQueue _queue;
    private readonly IMemoryCache _cache;
    private readonly string _baseUrl;

    public AccountEmails(UserManager<User> userManager, EmailQueue queue, IMemoryCache cache, IConfiguration configuration)
    {
        _userManager = userManager;
        _queue = queue;
        _cache = cache;
        _baseUrl = configuration["App:BaseUrl"]!.TrimEnd('/');
    }

    public async Task SendConfirmationAsync(User user)
    {
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

        Send(user.Email!, "Confirm your email for Reelr", $"""
            Hi {user.UserName},

            Open this link to confirm your email address and start using Reelr:

            {Link("confirm-email", user.Id, token)}

            The link works for 3 hours. If you did not create a Reelr account, you can ignore this email.
            """);
    }

    public async Task SendPasswordResetAsync(User user)
    {
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        Send(user.Email!, "Reset your Reelr password", $"""
            Hi {user.UserName},

            Open this link to choose a new password:

            {Link("reset-password", user.Id, token)}

            The link works for 3 hours. If you did not ask for this, you can ignore this email; your password stays the same.
            """);
    }

    // Sent instead of a confirmation when the address already has an account, so the sign-up form
    // answers the same either way and does not reveal which addresses are registered.
    public void SendAlreadyRegistered(string email)
    {
        Send(email, "You already have a Reelr account", $"""
            Hi,

            Someone, probably you, tried to create a Reelr account with this email address, but it already has one.

            Log in at {_baseUrl}/login, or reset your password at {_baseUrl}/forgot-password if you have forgotten it.

            If this was not you, you can ignore this email.
            """);
    }

    private string Link(string page, int userId, string token) =>
        $"{_baseUrl}/{page}#userId={userId}&token={Uri.EscapeDataString(token)}";

    private void Send(string to, string subject, string body)
    {
        var key = $"account-email:{to.Trim().ToUpperInvariant()}";
        var sent = _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return new SentCount();
        })!;

        if (Interlocked.Increment(ref sent.Value) > MaxPerAddressPerHour)
        {
            return;
        }

        _queue.Enqueue(new EmailMessage(to, subject, body));
    }

    private sealed class SentCount
    {
        public int Value;
    }
}
