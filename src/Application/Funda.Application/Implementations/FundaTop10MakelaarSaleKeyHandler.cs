using Funda.Infrastructure.Cache.Contracts;
using Funda.Infrastructure.Client.Contracts;
using Microsoft.Extensions.Logging;

namespace Funda.Application.Implementations;

public class FundaTop10MakelaarSaleKeyHandler(
    ICacheService cacheService,
    IFundaApiClient fundaClient,
    ISlidingWindowRateLimiter slidingWindowRateLimiter,
    ILogger<FundaTop10MakelaarKeyHandler> logger) : IRedisKeyEventHandler
{
    public IList<string> KeyPatterns { get; } = new List<string>
        { "funda:Makelaar:sale:top10", "funda:Makelaar:sale:top10:rate-limit" };

    public async Task HandleAsync(string eventType, CancellationToken cancellationToken)
    {
        await using var redLock = await cacheService.AcquireLock($"{KeyPatterns[0]}:lock", TimeSpan.FromMinutes(5));

        if (!redLock.IsAcquired)
        {
            logger.LogWarning("Could not acquire lock for key {Key}", KeyPatterns[0]);
            return;
        }

        var isProgressing = await cacheService.KeyExist("funda:Makelaar:sale:top10:progress");

        if (await cacheService.KeyExist(KeyPatterns[0]) && !isProgressing)
        {
            logger.LogInformation("Top 10 makelaars already processed, skipping.");
            return;
        }

        string? url = null;
        if (isProgressing)
        {
            url = await cacheService.Get("funda:Makelaar:sale:top10:progress");
            if (string.IsNullOrWhiteSpace(url))
            {
                url = null;
            }
        }

        logger.LogInformation("Processing {Key} for event {EventType}, starting at URL: {Url}", KeyPatterns[0],
            eventType,
            url ?? "first page");

        var success = true;

        try
        {
            do
            {
                var allowed = await slidingWindowRateLimiter.TryAcquire(
                    "funda:Makelaar:sale:top10:rate-limit",
                    100,
                    TimeSpan.FromMinutes(1));

                if (!allowed)
                {
                    logger.LogWarning("Rate limit exceeded during loop for key {Key} at URL {Url}", KeyPatterns[0],
                        url ?? "first page");

                    await cacheService.Set("funda:Makelaar:sale:top10:progress", url ?? string.Empty);

                    success = false;
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
                    {
                        MakelaarId = g.Key!,
                        Count = g.Count(),
                        MakelaarNaam = g.First().MakelaarNaam!
                    });

                foreach (var group in groupedByMakelaar)
                {
                    await cacheService.SortedSetIncrement(KeyPatterns[0], group.MakelaarNaam, group.Count);
                }

                url = response.Paging?.VolgendeUrl;

                await cacheService.Set("funda:Makelaar:sale:top10:progress", url ?? string.Empty);
            } while (!string.IsNullOrEmpty(url) && !cancellationToken.IsCancellationRequested);
        }
        catch (Exception ex)
        {
            success = false;
            logger.LogError(ex, "Exception occurred during Top 10 makelaars processing.");
            throw;
        }

        if (success)
        {
            logger.LogInformation("Successfully stored Top 10 makelaars into Redis under key {Key}", KeyPatterns[0]);
            await cacheService.KeyExpire(KeyPatterns[0], TimeSpan.FromMinutes(720));
            await cacheService.DeleteByKey("funda:Makelaar:sale:top10:progress");
        }
        else
        {
            logger.LogInformation("Processing did not complete; progress key retained for retry.");
        }
    }
}