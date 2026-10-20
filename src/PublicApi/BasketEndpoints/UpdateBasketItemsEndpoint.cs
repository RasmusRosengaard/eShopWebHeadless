using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.PublicApi.Services;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.BasketEndpoints;

public class UpdateBasketItemsRequest : BaseRequest
{
    public string BuyerId { get; set; } = string.Empty;

    /// <summary>Basket item id -> new quantity. Quantity 0 removes the item.</summary>
    public Dictionary<string, int> Quantities { get; set; } = new();
}

/// <summary>
/// Sets quantities on the current buyer's basket items
/// </summary>
public class UpdateBasketItemsEndpoint : IEndpoint<IResult, UpdateBasketItemsRequest, BasketApiService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPut("api/basket/items",
            async (UpdateBasketItemsRequest request, HttpContext context, BasketApiService basketService) =>
            {
                var buyerId = await BuyerIdentity.ResolveAsync(context);
                if (buyerId == null) return Results.BadRequest();
                request.BuyerId = buyerId;
                return await HandleAsync(request, basketService);
            })
            .Produces<BasketDto>()
            .WithTags("BasketEndpoints");
    }

    public async Task<IResult> HandleAsync(UpdateBasketItemsRequest request, BasketApiService basketService)
    {
        var basket = await basketService.SetQuantitiesAsync(request.BuyerId, request.Quantities);
        return basket == null ? Results.NotFound() : Results.Ok(basket);
    }
}
