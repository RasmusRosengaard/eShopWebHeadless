using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;

namespace Microsoft.eShopWeb.PublicApi.Services;

/// <summary>
/// Resolves who owns the basket for the current request. Replaces the basket cookie that Web used:
/// logged-in users are identified by their JWT name claim, anonymous users by a GUID the SPA sends in a header.
/// </summary>
public static class BuyerIdentity
{
    public const string ANONYMOUS_ID_HEADER = "X-Anonymous-Id";

    public static async Task<string?> ResolveAsync(HttpContext context)
    {
        // Basket endpoints allow anonymous callers, so the JWT is not validated by [Authorize].
        // Authenticate explicitly, because AddIdentity makes the Identity cookie the default scheme.
        var result = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        var userName = result.Succeeded ? result.Principal.Identity?.Name : null;
        if (!string.IsNullOrEmpty(userName))
        {
            return userName;
        }

        // Only accept a GUID, so an anonymous caller cannot address a real user's basket by username.
        var anonymousId = context.Request.Headers[ANONYMOUS_ID_HEADER].ToString();
        return Guid.TryParse(anonymousId, out _) ? anonymousId : null;
    }
}
