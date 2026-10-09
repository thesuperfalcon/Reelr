using Microsoft.Extensions.Caching.Memory;

namespace backend.Features.Movies;

// Keeps TMDB answers in memory for a while, since film data changes slowly and every page view would
// otherwise call TMDB again. It has its own size limit, counted in characters of TMDB's JSON, so many
// different searches cannot fill the server's memory. "Tmdb:CacheSizeMegabytes" sets the limit; 0 turns it off.
public sealed class TmdbCache : IDisposable
{
    private readonly MemoryCache? _cache;

    public TmdbCache(IConfiguration configuration)
    {
        var sizeMegabytes = configuration.GetValue("Tmdb:CacheSizeMegabytes", 50);

        if (sizeMegabytes > 0)
        {
            _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = sizeMegabytes * 1024L * 1024L });
        }
    }

    public bool TryGet<T>(string key, out T? value) where T : class
    {
        value = null;
        return _cache != null && _cache.TryGetValue(key, out value);
    }

    public void Set<T>(string key, T value, int size, TimeSpan duration) where T : class
    {
        _cache?.Set(key, value, new MemoryCacheEntryOptions
        {
            Size = size,
            AbsoluteExpirationRelativeToNow = duration
        });
    }

    public void Dispose() => _cache?.Dispose();
}
