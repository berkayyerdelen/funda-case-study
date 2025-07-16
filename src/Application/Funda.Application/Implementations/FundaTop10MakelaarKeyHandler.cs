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

    public IReadOnlyCollection<string> KeyPatterns { get; } = ["funda:Makelaar:top10", "funda:Makelaar:top10:rate-limit"];

   public async Task HandleAsync(string eventType, CancellationToken cancellationToken)
{
    await using var redLock = await cacheService.AcquireLock($"{Key}:lock", TimeSpan.FromMinutes(5));

    if (!redLock.IsAcquired)
    {
        logger.LogWarning("Could not acquire lock for key {Key}", Key);
        return;
    }

    var isProgressing = await cacheService.KeyExist("funda:Makelaar:top10:progress");

    if (await cacheService.KeyExist(Key) && !isProgressing)
    {
        logger.LogInformation("Top 10 makelaars already processed, skipping.");
        return;
    }

    // Start from saved progress URL or null (first page)
    string? url = null;
    if (isProgressing)
    {
        url = await cacheService.Get("funda:Makelaar:top10:progress");
        // Defensive: if empty string stored, treat as null for first page
        if (string.IsNullOrWhiteSpace(url))
        {
            url = null;
        }
    }

    logger.LogInformation("Processing {Key} for event {EventType}, starting at URL: {Url}", Key, eventType, url ?? "first page");

    var success = true;

    try
    {
        do
        {
            var allowed = await slidingWindowRateLimiter.TryAcquire(
                "funda:Makelaar:top10:rate-limit",
                100,
                TimeSpan.FromMinutes(1));

            if (!allowed)
            {
                logger.LogWarning("Rate limit exceeded during loop for key {Key} at URL {Url}", Key, url ?? "first page");

                // Save progress to continue next time
                await cacheService.Set("funda:Makelaar:top10:progress", url ?? string.Empty);

                success = false;
                break;
            }

            var response = await fundaClient.GetFeeds(url, cancellationToken);

            if (response?.Objects == null || response.Objects.Count == 0)
            {
                // No more data to process
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
                await cacheService.SortedSetIncrement(Key, group.MakelaarNaam, group.Count);
            }

            // Move to next page URL
            url = response.Paging?.VolgendeUrl;

            // Save progress AFTER successful processing of this page
            await cacheService.Set("funda:Makelaar:top10:progress", url ?? string.Empty);

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
        logger.LogInformation("Successfully stored Top 10 makelaars into Redis under key {Key}", Key);
        // Optionally set TTL for final result
        await cacheService.KeyExpire(Key, TimeSpan.FromMinutes(720));
        // Delete progress key ONLY on full success
        await cacheService.DeleteByKey("funda:Makelaar:top10:progress");
    }
    else
    {
        logger.LogInformation("Processing did not complete; progress key retained for retry.");
    }
}
}
