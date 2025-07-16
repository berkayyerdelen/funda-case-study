namespace Funda.Infrastructure.Cache.Contracts;

public interface ISlidingWindowRateLimiter
{
    Task<bool> TryAcquire(string key, int limit, TimeSpan window);
}