using Funda.Infrastructure.Client.Contracts;
using Funda.Infrastructure.Client.Responses;
using RedLockNet;

namespace Funda.Application.Tests;

using Implementations;
using Infrastructure.Cache.Contracts;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class FundaTop10MakelaarKeyHandlerTests
{
    private readonly ICacheService _cacheService;
    private readonly IFundaApiClient _fundaClient;
    private readonly ISlidingWindowRateLimiter _rateLimiter;
    private readonly ILogger<FundaTop10MakelaarKeyHandler> _logger;
    private readonly FundaTop10MakelaarKeyHandler _handler;

    public FundaTop10MakelaarKeyHandlerTests()
    {
        _cacheService = Substitute.For<ICacheService>();
        _fundaClient = Substitute.For<IFundaApiClient>();
        _rateLimiter = Substitute.For<ISlidingWindowRateLimiter>();
        _logger = Substitute.For<ILogger<FundaTop10MakelaarKeyHandler>>();
        _handler = new FundaTop10MakelaarKeyHandler(
            _cacheService,
            _fundaClient,
            _rateLimiter,
            _logger);
    }

    [Fact]
    public async Task HandleAsync_WhenLockCannotBeAcquired_ReturnsEarly()
    {
        // Arrange
        var redLock = Substitute.For<IRedLock>();
        redLock.IsAcquired.Returns(false);
        _cacheService.AcquireLock(Arg.Any<string>(), Arg.Any<TimeSpan>()).Returns(redLock);

        // Act
        await _handler.Handle("keyevent", CancellationToken.None);

        // Assert
        await _fundaClient.DidNotReceive().GetFeeds(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenTop10AlreadyProcessed_ReturnsEarly()
    {
        // Arrange
        var redLock = Substitute.For<IRedLock>();
        redLock.IsAcquired.Returns(true);
        _cacheService.AcquireLock(Arg.Any<string>(), Arg.Any<TimeSpan>()).Returns(redLock);
        _cacheService.KeyExist("funda:Makelaar:top10:Makelaar").Returns(true);
        _cacheService.KeyExist("funda:Makelaar:top10:progress").Returns(false);

        // Act
        await _handler.Handle("keyevent", CancellationToken.None);

        // Assert
        await _fundaClient.DidNotReceive().GetFeeds(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenRateLimitExceeded_StopsProcessing()
    {
        // Arrange
        var redLock = Substitute.For<IRedLock>();
        redLock.IsAcquired.Returns(true);
        _cacheService.AcquireLock(Arg.Any<string>(), Arg.Any<TimeSpan>()).Returns(redLock);
        _cacheService.KeyExist(Arg.Any<string>()).Returns(false);
        _rateLimiter.TryAcquire(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>()).Returns(false);

        // Act
        await _handler.Handle("keyevent", CancellationToken.None);

        // Assert
        await _fundaClient.DidNotReceive().GetFeeds(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_HappyPath_ProcessesAndStoresMakelaars()
    {
        // Arrange
        var redLock = Substitute.For<IRedLock>();
        redLock.IsAcquired.Returns(true);
        _cacheService.AcquireLock(Arg.Any<string>(), Arg.Any<TimeSpan>()).Returns(redLock);
        _cacheService.KeyExist(Arg.Any<string>()).Returns(false);
        _rateLimiter.TryAcquire(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>()).Returns(true);

        var response = new FundaApiResponse
        {
            Objects = new List<FundaObject>
            {
                new() { MakelaarId = 123, MakelaarNaam = "Makelaar1" },
                new() { MakelaarId = 123, MakelaarNaam = "Makelaar1" },
                new() { MakelaarId = 456, MakelaarNaam = "Makelaar2" }
            },
            Paging = null
        };

        _fundaClient.GetFeeds(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(response);

        // Act
        await _handler.Handle("keyevent", CancellationToken.None);

        // Assert
        await _cacheService.Received().SortedSetIncrement("funda:Makelaar:top10", "Makelaar1", 2);
        await _cacheService.Received().SortedSetIncrement("funda:Makelaar:top10", "Makelaar2", 1);
        await _cacheService.Received().DeleteByKey("funda:Makelaar:top10:progress");
    }

    [Fact]
    public async Task HandleAsync_WithPagination_ProcessesAllPages()
    {
        // Arrange
        var redLock = Substitute.For<IRedLock>();
        redLock.IsAcquired.Returns(true);
        _cacheService.AcquireLock(Arg.Any<string>(), Arg.Any<TimeSpan>()).Returns(redLock);
        _cacheService.KeyExist(Arg.Any<string>()).Returns(false);
        _rateLimiter.TryAcquire(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<TimeSpan>()).Returns(true);

        var response1 = new FundaApiResponse()
        {
            Objects = new List<FundaObject>
            {
                new() { MakelaarId = 123, MakelaarNaam = "Makelaar1" }
            },
            Paging = new Paging { VolgendeUrl = "page2" }
        };

        var response2 = new FundaApiResponse
        {
            Objects = new List<FundaObject>
            {
                new() { MakelaarId = 456, MakelaarNaam = "Makelaar2" }
            },
            Paging = null
        };

        _fundaClient.GetFeeds(null, Arg.Any<CancellationToken>()).Returns(response1);
        _fundaClient.GetFeeds("page2", Arg.Any<CancellationToken>()).Returns(response2);

        // Act
        await _handler.Handle("keyevent", CancellationToken.None);

        // Assert
        await _fundaClient.Received().GetFeeds(null, Arg.Any<CancellationToken>());
        await _fundaClient.Received().GetFeeds("page2", Arg.Any<CancellationToken>());
        await _cacheService.Received().SortedSetIncrement("funda:Makelaar:top10", "Makelaar1", 1);
        await _cacheService.Received().SortedSetIncrement("funda:Makelaar:top10", "Makelaar2", 1);
    }
}