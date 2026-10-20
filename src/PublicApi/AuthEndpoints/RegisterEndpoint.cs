using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Identity;
using MinimalApi.Endpoint;

namespace Microsoft.eShopWeb.PublicApi.AuthEndpoints;

public class RegisterRequest : BaseRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Creates a user and returns a token, so the SPA is logged in straight away.
/// Replaces Web's Identity/Account/Register page (email confirmation was a no-op there and is left out).
/// </summary>
public class RegisterEndpoint : IEndpoint<IResult, RegisterRequest, UserManager<ApplicationUser>, ITokenClaimsService>
{
    public void AddRoute(IEndpointRouteBuilder app)
    {
        app.MapPost("api/register",
            async (RegisterRequest request, UserManager<ApplicationUser> userManager, ITokenClaimsService tokenClaimsService) =>
            {
                return await HandleAsync(request, userManager, tokenClaimsService);
            })
            .Produces<AuthenticateResponse>()
            .WithTags("AuthEndpoints");
    }

    public async Task<IResult> HandleAsync(RegisterRequest request, UserManager<ApplicationUser> userManager, ITokenClaimsService tokenClaimsService)
    {
        var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return Results.BadRequest(result.Errors.Select(e => e.Description));
        }

        return Results.Ok(new AuthenticateResponse(request.CorrelationId())
        {
            Result = true,
            Username = request.Email,
            Token = await tokenClaimsService.GetTokenAsync(request.Email)
        });
    }
}
