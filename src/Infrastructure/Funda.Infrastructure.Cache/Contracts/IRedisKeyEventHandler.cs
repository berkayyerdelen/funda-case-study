namespace Funda.Infrastructure.Cache.Contracts;

public interface IRedisKeyEventHandler
{
    string Key { get; }
    Task HandleAsync(string eventType, CancellationToken cancellationToken);
}