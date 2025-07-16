namespace Funda.Infrastructure.Cache.Contracts;

public interface IRedisKeyEventHandler
{
    IReadOnlyCollection<string> KeyPatterns { get; }
    Task HandleAsync(string eventType, CancellationToken cancellationToken);
}