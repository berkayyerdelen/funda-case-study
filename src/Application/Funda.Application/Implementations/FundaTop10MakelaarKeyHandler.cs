using Funda.Infrastructure.Cache.Contracts;
using Funda.Infrastructure.Client.Contracts;
using Microsoft.Extensions.Logging;

namespace Funda.Application.Implementations;

public class FundaTop10MakelaarKeyHandler(
    ICacheService cacheService,
    IFundaApiClient fundaClient,
    ISlidingWindowRateLimiter slidingWindowRateLimiter,
    ILogger<FundaTop10MakelaarKeyHandler> logger) : IRedisKeyEventHandler
{
    public string Key => "funda:Makelaar:top10";

    public async Task HandleAsync(string eventType, CancellationToken cancellationToken)
    {
        await using var redLock = await cacheService.AcquireLock($"{Key}:lock", TimeSpan.FromMinutes(5));

        if (!redLock.IsAcquired)
        {
            logger.LogWarning("Could not acquire lock for key {Key}", Key);
            return;
        }


        var isProgressing = await cacheService.KeyExist("funda:Makelaar:top10:progress");

        if (await cacheService.KeyExist("funda:Makelaar:top10:Makelaar") && !isProgressing)
        {
            logger.LogInformation("Top 10 makelaars already processed, skipping.");
            return;
        }

        string? url = null;
        if (isProgressing)
        {
            url = await cacheService.Get("funda:Makelaar:top10:progress");
        }

        logger.LogInformation("Processing {Key} for event {EventType}", Key, eventType);

        do
        {
            var allowed =
                await slidingWindowRateLimiter.TryAcquire("funda:Makelaar:top10:rate-limit", 100,
                    TimeSpan.FromMinutes(1));

            if (!allowed)
            {
                logger.LogWarning("Rate limit exceeded during loop for key {Key}", Key);
                break;
            }

            var response = await fundaClient.GetFeeds(url, cancellationToken);

            if (response?.Objects == null || response.Objects.Count == 0)
            {
                break;
            }

            var groupedByMakelaar = response.Objects
                .Where(o => !string.IsNullOrWhiteSpace(o.MakelaarNaam))
                .GroupBy(o => o.MakelaarId)
                .Select(g => new
                    { MakelaarId = g.Key!, Count = g.Count(), MakelaarNaam = g.First().MakelaarNaam! });

            foreach (var group in groupedByMakelaar)
            {
                await cacheService.SortedSetIncrement(Key, group.MakelaarNaam, group.Count);
            }

            await cacheService.Set("funda:Makelaar:top10:progress", url ?? string.Empty);
            url = response.Paging?.VolgendeUrl;
        } while (!string.IsNullOrEmpty(url) && !cancellationToken.IsCancellationRequested);


        logger.LogInformation("Stored Top 10 makelaars into Redis under key {Key}", Key);
        await cacheService.KeyExpire("funda:Makelaar:top10:progress", TimeSpan.FromMinutes(720));
        await cacheService.DeleteByKey("funda:Makelaar:top10:progress");
    }
}