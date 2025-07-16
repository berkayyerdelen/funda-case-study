namespace Funda.Infrastructure.Cache.Contracts;

public interface IRedisKeyEventHandler
{
    IList<string> KeyPatterns { get; }
    Task HandleAsync(string eventType, CancellationToken cancellationToken);
}