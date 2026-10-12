using System.Security.Cryptography;
using backend.Data;
using backend.Features.Users;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.Auth;

/// <summary>
/// Issues, rotates and revokes refresh tokens. The token lives in a cookie the page's scripts cannot read
/// (HttpOnly), that is only sent over HTTPS (Secure), only to /auth and only from Reelr's own pages
/// (SameSite=Strict). So a script injected into the page can use the short access token while the page is
/// open, but cannot take the sign-in with it.
/// </summary>
public class RefreshTokens
{
    public const string CookieName = "__Secure-reelr-refresh";

    // Required on the endpoints that read the cookie. Another site cannot set a custom header on a request
    // to Reelr without CORS approval, which Reelr never gives, so the header is a second guard against CSRF.
    public const string CsrfHeader = "X-Reelr-Csrf";

    private const string CookiePath = "/auth";

    // Two tabs can send the same token at once. The second one within this window gets its own new token
    // instead of being treated as a stolen copy.
    private static readonly TimeSpan ReuseGrace = TimeSpan.FromSeconds(20);

    private readonly ReelrContext _context;
    private readonly IConfiguration _configuration;
    private readonly SecurityLog _securityLog;

    public RefreshTokens(ReelrContext context, IConfiguration configuration, SecurityLog securityLog)
    {
        _context = context;
        _configuration = configuration;
        _securityLog = securityLog;
    }

    private TimeSpan Lifetime => TimeSpan.FromDays(_configuration.GetValue("Jwt:RefreshDays", 30));

    /// <summary>Starts a new family, e.g. at login, and sets the cookie.</summary>
    public Task IssueAsync(HttpResponse response, User user) => AddAsync(response, user, Guid.NewGuid());

    /// <summary>
    /// Exchanges the cookie's token for a new one and returns its user, or null when the token is missing,
    /// unknown, expired, revoked, from before a security change, or already used.
    /// </summary>
    public async Task<User?> RotateAsync(HttpRequest request, HttpResponse response)
    {
        var token = await FindAsync(request);
        var now = DateTime.UtcNow;

        if (token == null || token.RevokedAt != null || token.ExpiresAt <= now || token.SecurityStamp != token.User.SecurityStamp)
        {
            Clear(response);
            return null;
        }

        // Claims the token in one statement, so two requests with the same token cannot both pass as first use.
        var claimed = await _context.RefreshTokens
            .Where(t => t.Id == token.Id && t.UsedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.UsedAt, now)) == 1;

        if (!claimed)
        {
            var usedAt = await _context.RefreshTokens.Where(t => t.Id == token.Id).Select(t => t.UsedAt).SingleAsync();

            if (usedAt == null || now - usedAt.Value > ReuseGrace)
            {
                await RevokeFamilyAsync(token.FamilyId, now);
                _securityLog.RefreshTokenReused(token.UserId);
                Clear(response);
                return null;
            }
        }

        await AddAsync(response, token.User, token.FamilyId);
        return token.User;
    }

    /// <summary>Ends the cookie's sign-in, for logout. Other devices stay signed in.</summary>
    public async Task<int?> RevokeAsync(HttpRequest request, HttpResponse response)
    {
        var token = await FindAsync(request);
        Clear(response);

        if (token == null)
        {
            return null;
        }

        await RevokeFamilyAsync(token.FamilyId, DateTime.UtcNow);
        return token.UserId;
    }

    private async Task AddAsync(HttpResponse response, User user, Guid familyId)
    {
        var now = DateTime.UtcNow;
        var value = Base64UrlToken();

        // Tokens past their end date can go; used ones are kept until then, to recognise a stolen copy.
        await _context.RefreshTokens.Where(t => t.UserId == user.Id && t.ExpiresAt < now).ExecuteDeleteAsync();

        var token = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = Hash(value),
            FamilyId = familyId,
            SecurityStamp = user.SecurityStamp ?? string.Empty,
            CreatedAt = now,
            ExpiresAt = now + Lifetime
        };

        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync();

        response.Cookies.Append(CookieName, value, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            Expires = token.ExpiresAt
        });
    }

    private async Task<RefreshToken?> FindAsync(HttpRequest request)
    {
        var value = request.Cookies[CookieName];

        if (string.IsNullOrEmpty(value) || value.Length > 100)
        {
            return null;
        }

        var hash = Hash(value);
        return await _context.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash);
    }

    private Task RevokeFamilyAsync(Guid familyId, DateTime now) =>
        _context.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.RevokedAt, now));

    private static void Clear(HttpResponse response) =>
        response.Cookies.Delete(CookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath
        });

    private static string Base64UrlToken() =>
        System.Buffers.Text.Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
}
