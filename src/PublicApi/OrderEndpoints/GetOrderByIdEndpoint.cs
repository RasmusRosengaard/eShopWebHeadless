using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.OrderEndpoints;

public class GetOrderByIdRequest : BaseRequest
{
    public int OrderId { get; init; }
    public string BuyerId { get; init; } = string.Empty;
}

/// <summary>
/// Gets one of the logged-in user's orders with items. Moved from Web's GetOrderDetailsHandler.
/// </summary>
public class GetOrderByIdEndpoint : IEndpoint<IResult, GetOrderByIdRequest, IReadRepository<Order>>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapGet("api/orders/{orderId}",
            [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] async
            (int orderId, ClaimsPrincipal user, IReadRepository<Order> orderRepository) =>
            {
                return await HandleAsync(new GetOrderByIdRequest { OrderId = orderId, BuyerId = user.Identity!.Name! }, orderRepository);
            })
            .Produces<OrderDto>()
            .WithTags("OrderEndpoints");
    }

    public async Task<IResult> HandleAsync(GetOrderByIdRequest request, IReadRepository<Order> orderRepository)
    {
        var order = await orderRepository.FirstOrDefaultAsync(new OrderWithItemsByIdSpec(request.OrderId));

        // Don't reveal other users' orders.
        if (order == null || order.BuyerId != request.BuyerId) return Results.NotFound();

        return Results.Ok(OrderDto.FromOrder(order, includeItems: true));
    }
}
