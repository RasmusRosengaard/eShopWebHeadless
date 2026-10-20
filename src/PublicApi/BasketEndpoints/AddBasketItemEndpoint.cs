using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.PublicApi.Services;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.BasketEndpoints;

public class AddBasketItemRequest : BaseRequest
{
    public string BuyerId { get; set; } = string.Empty;
    public int CatalogItemId { get; set; }
    public int Quantity { get; set; } = 1;
}

/// <summary>
/// Adds a catalog item to the current buyer's basket (creates the basket if needed)
/// </summary>
public class AddBasketItemEndpoint : IEndpoint<IResult, AddBasketItemRequest, BasketApiService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/basket/items",
            async (AddBasketItemRequest request, HttpContext context, BasketApiService basketService) =>
            {
                var buyerId = await BuyerIdentity.ResolveAsync(context);
                if (buyerId == null) return Results.BadRequest();
                request.BuyerId = buyerId;
                return await HandleAsync(request, basketService);
            })
            .Produces<BasketDto>()
            .WithTags("BasketEndpoints");
    }

    public async Task<IResult> HandleAsync(AddBasketItemRequest request, BasketApiService basketService)
    {
        if (request.Quantity < 1) return Results.BadRequest();

        var basket = await basketService.AddItemAsync(request.BuyerId, request.CatalogItemId, request.Quantity);
        return basket == null ? Results.NotFound() : Results.Ok(basket);
    }
}
