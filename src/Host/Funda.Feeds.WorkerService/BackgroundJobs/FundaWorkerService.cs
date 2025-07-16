using Funda.Infrastructure.Cache.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Funda.Feeds.WorkerService.BackgroundJobs;

public class FundaWorkerService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IEnumerable<IRedisKeyEventHandler> _handlers;
    private readonly ILogger<FundaWorkerService> _logger;
    private static readonly string[] Channels = ["__keyevent@0__:expired", "__keyevent@0__:del"];

    public FundaWorkerService(ILogger<FundaWorkerService> logger, IConnectionMultiplexer redis, IEnumerable<IRedisKeyEventHandler> handlers)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _redis = redis;
        _handlers = handlers;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = _redis.GetSubscriber();

        foreach (var channel in Channels)
        {
            await subscriber.SubscribeAsync(channel, async (redisChannel, message) =>
            {
                var key = (string)message;
                var handler = _handlers.FirstOrDefault(h => h.KeyPatterns.Contains(key));

                if (handler != null)
                {
                    _logger.LogInformation("Received Redis event '{Channel}' for key '{Key}'", redisChannel, key);
                    await handler.Handle(redisChannel, stoppingToken);
                }
            });
        }

        await Task.CompletedTask;
    }
}