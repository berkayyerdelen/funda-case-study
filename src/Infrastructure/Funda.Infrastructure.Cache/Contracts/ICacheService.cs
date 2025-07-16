using RedLockNet;

namespace Funda.Infrastructure.Cache.Contracts;

public interface ICacheService
{
    Task<IRedLock> AcquireLock(string key, TimeSpan expiry);
    Task DeleteByKey(string key);
    Task SortedSetIncrement(string key, string member, double increment);
    Task Set(string key, string data, TimeSpan? fromMinutes = default);
    Task<IEnumerable<(string Member, double Score)>> GetTopFromSortedSetAsync(string key, int count);
    Task<bool> KeyExist(string key);
    Task<string> Get(string key);
    Task KeyExpire(string key, TimeSpan fromMinutes = default);
}