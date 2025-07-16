using System.Text.Json;
using System.Text.RegularExpressions;
using Funda.Infrastructure.Client.Configuration;
using Funda.Infrastructure.Client.Contracts;
using Funda.Infrastructure.Client.Responses;
using Microsoft.Extensions.Options;

public class FundaApiClient : IFundaApiClient
{
    private readonly HttpClient _httpClient;
    private readonly FundaApiOptions _options;

    public FundaApiClient(HttpClient httpClient, IOptions<FundaApiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<FundaApiResponse?> GetFeeds(string? nextPageUrl = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string requestUrl;

            if (string.IsNullOrEmpty(nextPageUrl))
            {
                // Compose the first page URL with your key inside the path + query params
                requestUrl = $"{_options.BaseUrl}{_options.ApiKey}/?type=koop&zo=/amsterdam/tuin/&page=1&pagesize=100";
            }
            else
            {
                var zoPath = ExtractZoFromVolgendeUrl(nextPageUrl);
                requestUrl = $"{_options.BaseUrl}{_options.ApiKey}/?type=koop&zo={zoPath}";
            }

            var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<FundaApiResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return data;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string ExtractZoFromVolgendeUrl(string volgendeUrl)
    {
        var match = Regex.Match(volgendeUrl, @"^/~/koop(?<zo>/.+?)/?$");
        return match.Success ? match.Groups["zo"].Value : "/amsterdam/tuin";
    }
}