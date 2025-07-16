using Funda.Infrastructure.Client.Responses;

namespace Funda.Infrastructure.Client.Contracts;

public interface IFundaApiClient
{
    Task<FundaApiResponse?> GetFeeds(string? nextPageUrl = null, CancellationToken cancellationToken = default);
}