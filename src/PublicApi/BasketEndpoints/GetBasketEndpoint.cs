using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.PublicApi.Services;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.BasketEndpoints;

/// <summary>
/// Gets the basket of the current buyer (JWT user or X-Anonymous-Id header)
/// </summary>
public class GetBasketEndpoint : IEndpoint<IResult, string, BasketApiService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/basket",
            async (HttpContext context, BasketApiService basketService) =>
            {
                var buyerId = await BuyerIdentity.ResolveAsync(context);
                return buyerId == null ? Results.BadRequest() : await HandleAsync(buyerId, basketService);
            })
            .Produces<BasketDto>()
            .WithTags("BasketEndpoints");
    }

    public async Task<IResult> HandleAsync(string buyerId, BasketApiService basketService)
    {
        return Results.Ok(await basketService.GetBasketAsync(buyerId));
    }
}
