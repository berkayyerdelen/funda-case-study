namespace Funda.Infrastructure.Cache.Contracts;

public interface IRedisKeyEventHandler
{
    IList<string> KeyPatterns { get; }
    Task Handle(string eventType, CancellationToken cancellationToken);
}