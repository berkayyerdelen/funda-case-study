using Funda.Api.Endpoints;
using Funda.Infrastructure.Cache.Configuration;
using Funda.Infrastructure.Cache.Contracts;
using Funda.Infrastructure.Cache.Implementations;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
var cacheOptions = builder.Configuration.GetSection("RedisCache").Get<CacheOptions>();
var multiplexer = ConnectionMultiplexer.Connect($"{cacheOptions.Host},abortConnect={cacheOptions.AbortConnect}");

builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
builder.Services.AddSingleton<RedLockFactory>(_ =>
    RedLockFactory.Create(new List<RedLockMultiplexer> { multiplexer }));

builder.Services.AddTransient<IDatabase>(sp =>
{
    var multiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
    return multiplexer.GetDatabase();
});
builder.Services.AddScoped<ICacheService, CacheService>();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapProductsEndpoints();
app.Run();