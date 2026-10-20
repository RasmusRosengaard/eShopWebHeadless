using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.PublicApi.Services;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.BasketEndpoints;

public class TransferBasketRequest : BaseRequest
{
    public string AnonymousId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
}

/// <summary>
/// Moves the anonymous basket to the logged-in user. The SPA calls it right after login
/// (Web did this inside its Login page model).
/// </summary>
public class TransferBasketEndpoint : IEndpoint<IResult, TransferBasketRequest, BasketApiService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/basket/transfer",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (TransferBasketRequest request, ClaimsPrincipal user, BasketApiService basketService) =>
            {
                request.UserName = user.Identity!.Name!;
                return await HandleAsync(request, basketService);
            })
            .WithTags("BasketEndpoints");
    }

    public async Task<IResult> HandleAsync(TransferBasketRequest request, BasketApiService basketService)
    {
        if (!Guid.TryParse(request.AnonymousId, out _)) return Results.BadRequest();

        await basketService.TransferAsync(request.AnonymousId, request.UserName);
        return Results.Ok();
    }
}
