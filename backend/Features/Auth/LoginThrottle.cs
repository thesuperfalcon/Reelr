using System.Net;
using Microsoft.Extensions.Caching.Memory;

namespace backend.Features.Auth;

/// <summary>
/// Counts wrong passwords per account and client IP. After a few from one address, that address may not try
/// that account again for a while, and its attempts no longer count towards Identity's account lockout.
/// Someone guessing from one address is stopped without locking the account for its owner, who signs in from
/// somewhere else. Identity's lockout (counted per account) still stops guessing spread over many addresses.
/// "RateLimiting:LoginPerAccount" (PermitLimit, WindowSeconds) sets the limit.
/// </summary>
public sealed class LoginThrottle : IDisposable
{
    // Bounds memory when many accounts and addresses are tried; each entry counts as 1.
    private const int MaxTrackedPairs = 100_000;

    private readonly MemoryCache _failures = new(new MemoryCacheOptions { SizeLimit = MaxTrackedPairs });
    private readonly int _maxFailures;
    private readonly TimeSpan _window;

    public LoginThrottle(IConfiguration configuration)
    {
        var section = configuration.GetSection("RateLimiting:LoginPerAccount");
        _maxFailures = section.GetValue("PermitLimit", 5);
        _window = TimeSpan.FromSeconds(section.GetValue("WindowSeconds", 900));
    }

    public TimeSpan Window => _window;

    public bool IsBlocked(string account, IPAddress? address) =>
        _failures.TryGetValue(Key(account, address), out FailureCount? count) && count!.Value >= _maxFailures;

    public void RecordFailure(string account, IPAddress? address)
    {
        // The window starts at the first failure, so the address can try again once it has passed.
        var count = _failures.GetOrCreate(Key(account, address), entry =>
        {
            entry.Size = 1;
            entry.AbsoluteExpirationRelativeToNow = _window;
            return new FailureCount();
        })!;

        Interlocked.Increment(ref count.Value);
    }

    public void Reset(string account, IPAddress? address) => _failures.Remove(Key(account, address));

    public void Dispose() => _failures.Dispose();

    private static string Key(string account, IPAddress? address) => $"{account}|{address}";

    private sealed class FailureCount
    {
        public int Value;
    }
}
