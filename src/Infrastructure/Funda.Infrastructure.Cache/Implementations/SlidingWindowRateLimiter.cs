using Funda.Infrastructure.Cache.Contracts;
using StackExchange.Redis;

namespace Funda.Infrastructure.Cache.Implementations;

public class SlidingWindowRateLimiter : ISlidingWindowRateLimiter
{
    private readonly IDatabase _cacheService;

    public SlidingWindowRateLimiter(IDatabase cacheService)
    {
        _cacheService = cacheService;
    }

    public async Task<bool> TryAcquire(string key, int limit, TimeSpan window)

    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var min = now - (long)window.TotalMilliseconds;

        await _cacheService.SortedSetRemoveRangeByScoreAsync(key, 0, min); // cleanup old entries

        var count = await _cacheService.SortedSetLengthAsync(key);

        if (count >= limit)
            return false;

        await _cacheService.SortedSetAddAsync(key, now.ToString(), now); // add current timestamp
        await _cacheService.KeyExpireAsync(key, window); // set expiry to prevent memory leak
        return true;
    }
}