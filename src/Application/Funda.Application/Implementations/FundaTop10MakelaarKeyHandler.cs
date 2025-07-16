using Funda.Infrastructure.Cache.Contracts;
using Funda.Infrastructure.Client.Contracts;
using Funda.Infrastructure.Client.Responses;
using Microsoft.Extensions.Logging;

namespace Funda.Application.Implementations;

public class FundaTop10MakelaarKeyHandler(
    ICacheService cacheService,
    IFundaApiClient fundaClient,
    ISlidingWindowRateLimiter slidingWindowRateLimiter,
    ILogger<FundaTop10MakelaarKeyHandler> logger) : IRedisKeyEventHandler
{
    private const string LeaderboardKey = "funda:Makelaar:top10";
    private const string RateLimitKey = "funda:Makelaar:top10:rate-limit";
    private const string ProgressKey = "funda:Makelaar:top10:progress";

    public IList<string> KeyPatterns { get; } = [LeaderboardKey, RateLimitKey];

    public async Task Handle(string eventType, CancellationToken cancellationToken)
    {
        if (await IsLockedByAnotherConsumer()) return;

        if (await cacheService.KeyExist(LeaderboardKey) &&
            !await cacheService.KeyExist(ProgressKey))
        {
            logger.LogInformation("Top 10 makelaars already processed, skipping.");
            return;
        }

        var url = await GetProgressUrl();

        logger.LogInformation("Processing {Key} for event {EventType}, starting at URL: {Url}", 
            LeaderboardKey, eventType, url ?? "first page");

        var success = await ProcessTop10Makelaars(url, cancellationToken);

        if (success)
        {
            logger.LogInformation("Successfully stored Top 10 makelaars into Redis under key {Key}", LeaderboardKey);
            await cacheService.KeyExpire(LeaderboardKey, TimeSpan.FromMinutes(720));
            await cacheService.DeleteByKey(ProgressKey);
        }
        else
        {
            logger.LogInformation("Processing did not complete; progress key retained for retry.");
        }
    }

    private async Task<bool> ProcessTop10Makelaars(string? url, CancellationToken cancellationToken)
    {
        bool success = true;

        do
        {
            if (!await TryProceedWithinRateLimit(url))
            {
                success = false;
                break;
            }

            var response = await fundaClient.GetFeeds(url, cancellationToken);

            if (response?.Objects == null || response.Objects.Count == 0)
                break;

            await UpdateLeaderboard(response);

            url = response.Paging?.VolgendeUrl;

            await cacheService.Set(ProgressKey, url ?? string.Empty);
        }
        while (!string.IsNullOrEmpty(url) && !cancellationToken.IsCancellationRequested);

        return success;
    }

    private async Task<bool> TryProceedWithinRateLimit(string? url)
    {
        var allowed = await slidingWindowRateLimiter.TryAcquire(
            RateLimitKey,
            100,
            TimeSpan.FromMinutes(1));

        if (!allowed)
        {
            logger.LogWarning("Rate limit exceeded during loop for key {Key} at URL {Url}", 
                LeaderboardKey, url ?? "first page");

            await cacheService.Set(ProgressKey, url ?? string.Empty);
            return false;
        }

        return true;
    }

    private async Task UpdateLeaderboard(FundaApiResponse response)
    {
        var groupedByMakelaar = response.Objects
            .Where(o => !string.IsNullOrWhiteSpace(o.MakelaarNaam))
            .GroupBy(o => o.MakelaarId)
            .Select(g => new
            {
                MakelaarId = g.Key!,
                Count = g.Count(),
                MakelaarNaam = g.First().MakelaarNaam!
            });

        foreach (var group in groupedByMakelaar)
        {
            await cacheService.SortedSetIncrement(LeaderboardKey, group.MakelaarNaam, group.Count);
        }
    }

    private async Task<string?> GetProgressUrl()
    {
        if (!await cacheService.KeyExist(ProgressKey))
            return null;

        var url = await cacheService.Get(ProgressKey);
        return string.IsNullOrWhiteSpace(url) ? null : url;
    }

    private async Task<bool> IsLockedByAnotherConsumer()
    {
        await using var redLock = await cacheService.AcquireLock($"{LeaderboardKey}:lock", TimeSpan.FromMinutes(5));

        if (!redLock.IsAcquired)
        {
            logger.LogWarning("Could not acquire lock for key {Key}", LeaderboardKey);
            return true;
        }

        return false;
    }
}
