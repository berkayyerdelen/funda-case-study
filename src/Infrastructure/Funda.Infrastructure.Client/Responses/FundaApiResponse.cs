namespace Funda.Infrastructure.Client.Responses;

public class FundaApiResponse
{
    public List<FundaObject> Objects { get; set; }
    public Paging Paging { get; set; }
}