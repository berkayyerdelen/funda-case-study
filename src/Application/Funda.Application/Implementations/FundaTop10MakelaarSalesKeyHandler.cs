using Funda.Infrastructure.Cache.Contracts;

namespace Funda.Application.Implementations;

public class FundaTop10MakelaarSalesKeyHandler: IRedisKeyEventHandler
{
    // This handler is responsible for handling key events related to the top 10 makelaar sales.
    // For now, it is a placeholder
    public IList<string> KeyPatterns { get; }
    public Task Handle(string eventType, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}