using backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace backend.Features.Auth;

/// <summary>
/// Makes tokens revocable. Each token carries the user's security stamp, which Identity changes on a password
/// change or reset, a rename, or "log out everywhere". A token whose stamp no longer matches is refused, and so is
/// one for a deleted user. Stamps are cached briefly so not every request reads the database; this API forgets its
/// cached stamp as soon as it changes one, so a change made here takes effect at once.
/// </summary>
public class SecurityStamps
{
    public const string ClaimType = "sstamp";

    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopes;

    public SecurityStamps(IMemoryCache cache, IServiceScopeFactory scopes)
    {
        _cache = cache;
        _scopes = scopes;
    }

    public async Task<bool> IsCurrentAsync(int userId, string? stamp)
    {
        if (string.IsNullOrEmpty(stamp))
        {
            return false;
        }

        var current = await _cache.GetOrCreateAsync(Key(userId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

            using var scope = _scopes.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ReelrContext>().Users
                .Where(u => u.Id == userId)
                .Select(u => u.SecurityStamp)
                .FirstOrDefaultAsync();
        });

        return current == stamp;
    }

    public void Forget(int userId) => _cache.Remove(Key(userId));

    private static string Key(int userId) => $"security-stamp:{userId}";
}
