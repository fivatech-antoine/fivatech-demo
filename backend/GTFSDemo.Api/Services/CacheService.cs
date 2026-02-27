using Microsoft.Extensions.Caching.Memory;

namespace GTFSDemo.Api.Services;

/// <summary>
/// Wrapper typé sur IMemoryCache — facilite le mock en tests et uniformise
/// la durée de cache par défaut (30 s).
/// </summary>
public class CacheService(IMemoryCache cache)
{
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromSeconds(30);

    public T? Get<T>(string key) =>
        cache.TryGetValue(key, out T? value) ? value : default;

    public void Set<T>(string key, T value, TimeSpan? expiry = null) =>
        cache.Set(key, value, expiry ?? DefaultExpiry);

    public T GetOrCreate<T>(string key, Func<T> factory, TimeSpan? expiry = null) =>
        cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = expiry ?? DefaultExpiry;
            return factory();
        })!;
}
