using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Exceptions;
using Microsoft.eShopWeb.PublicApi.Services;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

/// <summary>
/// Checks out the logged-in user's basket: creates an order and deletes the basket.
/// Moved from Web's Basket/Checkout page model.
/// </summary>
public class CreateOrderEndpoint : IEndpoint<IResult, string, BasketApiService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/orders",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (ClaimsPrincipal user, BasketApiService basketService) =>
            {
                return await HandleAsync(user.Identity!.Name!, basketService);
            })
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(string buyerId, BasketApiService basketService)
    {
        try
        {
            // Same hard-coded address as Web used. Collecting a real address is out of scope for the sample.
            var address = new Address("123 Main St.", "Kent", "OH", "United States", "44240");
            var created = await basketService.CheckoutAsync(buyerId, address);
            return created ? Results.Ok() : Results.BadRequest("Basket not found.");
        }
        catch (EmptyBasketOnCheckoutException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }
}
