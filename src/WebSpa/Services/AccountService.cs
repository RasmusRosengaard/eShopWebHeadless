using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using BlazorShared.Models;

namespace WebSpa.Services;

/// <summary>
/// Login, register and logout against PublicApi. Replaces Web's Identity pages.
/// </summary>
public class AccountService
{
    private readonly HttpClient _httpClient;
    private readonly AuthTokenStore _tokenStore;
    private readonly CustomAuthStateProvider _authStateProvider;
    private readonly BasketState _basketState;

    public AccountService(HttpClient httpClient,
        AuthTokenStore tokenStore,
        CustomAuthStateProvider authStateProvider,
        BasketState basketState)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
        _authStateProvider = authStateProvider;
        _basketState = basketState;
    }

    /// <returns>An error message, or null on success.</returns>
    public async Task<string> LoginAsync(string email, string password)
    {
        var response = await _httpClient.PostAsJsonAsync("authenticate", new { Username = email, Password = password });
        var result = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AuthenticateResponse>() : null;

        if (result?.IsLockedOut == true) return "User account locked out.";
        if (result?.Result != true) return "Invalid login attempt.";

        await SignInAsync(result.Token);
        return null;
    }

    /// <returns>Error messages, empty on success.</returns>
    public async Task<List<string>> RegisterAsync(string email, string password)
    {
        var response = await _httpClient.PostAsJsonAsync("register", new { Email = email, Password = password });
        if (!response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<List<string>>() ?? new List<string> { "Registration failed." };
        }

        var result = await response.Content.ReadFromJsonAsync<AuthenticateResponse>();
        await SignInAsync(result.Token);
        return new List<string>();
    }

    public async Task LogoutAsync()
    {
        _tokenStore.Token = null;
        _authStateProvider.NotifyUserChanged();
        await _basketState.RefreshAsync();
    }

    private async Task SignInAsync(string token)
    {
        _tokenStore.Token = token;

        // Same as Web's login: move the anonymous basket over to the user.
        await _httpClient.PostAsJsonAsync("basket/transfer", new { AnonymousId = _tokenStore.GetOrCreateAnonymousId() });

        _authStateProvider.NotifyUserChanged();
        await _basketState.RefreshAsync();
    }
}
