using Funda.Application.Implementations;
using Funda.Feeds.WorkerService.BackgroundJobs;
using Funda.Infrastructure.Cache.Configuration;
using Funda.Infrastructure.Cache.Contracts;
using Funda.Infrastructure.Cache.Implementations;
using Funda.Infrastructure.Client.Configuration;
using Funda.Infrastructure.Client.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
    })
    .ConfigureServices((hostContext, services) =>
    {
        // var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "redis";
        // var redisPort = Environment.GetEnvironmentVariable("REDIS_PORT") ?? "6379";
        
        services.Configure<FundaApiOptions>(hostContext.Configuration.GetSection("FundaApi"));
        
        var cacheOptions=  hostContext.Configuration.GetSection("RedisCache").Get<CacheOptions>();
       
        var multiplexer = ConnectionMultiplexer.Connect($"{cacheOptions.Host},abortConnect={cacheOptions.AbortConnect}");
        
        services.AddSingleton<ICacheService, CacheService>();
        services.AddSingleton<IRedisKeyEventHandler, FundaTop10MakelaarKeyHandler>();
        
        services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        
        services.AddSingleton<RedLockFactory>(_ =>
            RedLockFactory.Create(new List<RedLockMultiplexer> { multiplexer }));

        services.AddSingleton<IDatabase>(sp =>
        {
            var multiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
            return multiplexer.GetDatabase();
        });
        
        services.AddSingleton<ISlidingWindowRateLimiter, SlidingWindowRateLimiter>();
        
        services.AddHttpClient<IFundaApiClient, FundaApiClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHostedService<FundaWorkerJob>();
    })
    .Build();

await host.RunAsync();