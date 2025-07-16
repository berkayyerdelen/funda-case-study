using Funda.Api.Responses;
using Funda.Infrastructure.Cache.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Funda.Api.Endpoints;

public static class FeedsEndpoints
{
    public static WebApplication MapProductsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/makelaars/top10", async ([FromServices]ICacheService productService) =>
            {
                var top10Makelaar = await productService.GetTopFromSortedSetAsync("funda:Makelaar:top10", 10);

                return top10Makelaar.Select(x => new Top10MakelaarApiResponse()
                {
                    Name = x.Member,
                    Score = x.Score
                });
            })
            .WithDescription("Top 10 Makelaars")
            .Produces<IEnumerable<Top10MakelaarApiResponse>>(StatusCodes.Status200OK);

        return app;
    }
}