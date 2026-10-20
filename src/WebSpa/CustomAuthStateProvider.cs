using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Authorization;
using WebSpa.Services;

namespace WebSpa;

/// <summary>
/// Builds the current user from the JWT in localStorage.
/// Before, the user was fetched from Web's /User endpoint (cookie auth).
/// </summary>
public class CustomAuthStateProvider : AuthenticationStateProvider
{
    // JwtSecurityTokenHandler writes ClaimTypes.Name/Role as these short names.
    private const string NAME_CLAIM = "unique_name";
    private const string ROLE_CLAIM = "role";

    private readonly AuthTokenStore _tokenStore;

    public CustomAuthStateProvider(AuthTokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(new AuthenticationState(BuildUser(_tokenStore.Token)));
    }

    public void NotifyUserChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private static ClaimsPrincipal BuildUser(string token)
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        if (string.IsNullOrEmpty(token)) return anonymous;

        try
        {
            var claims = ParseClaims(token);
            var expired = claims.Find(c => c.Type == "exp") is { } exp
                && DateTimeOffset.FromUnixTimeSeconds(long.Parse(exp.Value)) < DateTimeOffset.UtcNow;
            if (expired) return anonymous;

            return new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt", NAME_CLAIM, ROLE_CLAIM));
        }
        catch (Exception)
        {
            return anonymous;
        }
    }

    private static List<Claim> ParseClaims(string token)
    {
        var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        using var json = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));

        var claims = new List<Claim>();
        foreach (var property in json.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var value in property.Value.EnumerateArray())
                {
                    claims.Add(new Claim(property.Name, value.ToString()));
                }
            }
            else
            {
                claims.Add(new Claim(property.Name, property.Value.ToString()));
            }
        }
        return claims;
    }
}
