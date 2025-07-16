using Funda.Infrastructure.Cache.Contracts;
using RedLockNet;
using RedLockNet.SERedis;
using StackExchange.Redis;

namespace Funda.Infrastructure.Cache.Implementations;

public class CacheService(RedLockFactory redLockFactory, IDatabase redisCache) : ICacheService
{
    public async Task<IRedLock> AcquireLock(string key, TimeSpan expiry)
    {
        return await redLockFactory.CreateLockAsync(key, expiry);
    }

    public async Task DeleteByKey(string key)
    {
        await redisCache.KeyDeleteAsync(key);
    }

    public async Task SortedSetIncrement(string key, string member, double increment)
    {
        await redisCache.SortedSetIncrementAsync(key, member, increment);
    }

    public async Task Set(string key, string data, TimeSpan? fromMinutes = default)
    {
        if (fromMinutes.HasValue)
        {
            await redisCache.StringSetAsync(key, data, fromMinutes.Value);
        }

        await redisCache.StringSetAsync(key, data);
    }

    public async Task<string> Get(string key)
    {
        return await redisCache.StringGetAsync(key);
    }

    public async Task<bool> KeyExist(string key)
    {
        return await redisCache.KeyExistsAsync(key);
    }

    public async Task<IEnumerable<(string Member, double Score)>> GetTopFromSortedSetAsync(string key, int count)
    {
        var entries = await redisCache.SortedSetRangeByRankWithScoresAsync(key, 0, count, Order.Descending);
        return entries.Select(e => (e.Element.ToString(), e.Score));
    }
}