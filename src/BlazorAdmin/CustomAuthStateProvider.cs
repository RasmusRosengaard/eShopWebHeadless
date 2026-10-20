using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Blazored.LocalStorage;
using BlazorShared;
using BlazorShared.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorAdmin;

/// <summary>
/// Builds the current user from a JWT issued by PublicApi (api/authenticate) and kept in localStorage.
/// Before, BlazorAdmin was hosted by Web and fetched the user from Web's /User endpoint (cookie auth).
/// </summary>
public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private const string TOKEN_KEY = "authToken";

    // JwtSecurityTokenHandler writes ClaimTypes.Name/Role as these short names.
    private const string NAME_CLAIM = "unique_name";
    private const string ROLE_CLAIM = "role";

    private readonly HttpClient _httpClient;
    private readonly ISyncLocalStorageService _localStorage;
    private readonly ILogger<CustomAuthStateProvider> _logger;
    private readonly string _apiUrl;

    public CustomAuthStateProvider(HttpClient httpClient,
        ISyncLocalStorageService localStorage,
        IOptions<BaseUrlConfiguration> baseUrlConfiguration,
        ILogger<CustomAuthStateProvider> logger)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _logger = logger;
        _apiUrl = baseUrlConfiguration.Value.ApiBase;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = _localStorage.GetItemAsString(TOKEN_KEY);
        var user = BuildUser(token);

        _httpClient.DefaultRequestHeaders.Authorization = user.Identity!.IsAuthenticated
            ? new AuthenticationHeaderValue("Bearer", token)
            : null;

        return Task.FromResult(new AuthenticationState(user));
    }

    /// <returns>An error message, or null on success.</returns>
    public async Task<string> LoginAsync(string email, string password)
    {
        AuthenticateResponse result = null;
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_apiUrl}authenticate", new { Username = email, Password = password });
            result = await response.Content.ReadFromJsonAsync<AuthenticateResponse>();
        }
        catch (Exception exc)
        {
            _logger.LogWarning(exc, "Login failed.");
        }

        if (result?.IsLockedOut == true) return "User account locked out.";
        if (result?.Result != true) return "Invalid login attempt.";

        _localStorage.SetItemAsString(TOKEN_KEY, result.Token);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        return null;
    }

    public void Logout()
    {
        _localStorage.RemoveItem(TOKEN_KEY);
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
