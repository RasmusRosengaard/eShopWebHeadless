using System;
using Blazored.LocalStorage;

namespace WebSpa.Services;

/// <summary>
/// Keeps the JWT and the anonymous basket id in localStorage.
/// Replaces the auth cookie and basket cookie that Web used.
/// </summary>
public class AuthTokenStore
{
    private const string TOKEN_KEY = "authToken";
    private const string ANONYMOUS_ID_KEY = "anonymousId";

    private readonly ISyncLocalStorageService _localStorage;

    public AuthTokenStore(ISyncLocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public string Token
    {
        get => _localStorage.GetItemAsString(TOKEN_KEY);
        set
        {
            if (string.IsNullOrEmpty(value)) _localStorage.RemoveItem(TOKEN_KEY);
            else _localStorage.SetItemAsString(TOKEN_KEY, value);
        }
    }

    public string GetOrCreateAnonymousId()
    {
        var anonymousId = _localStorage.GetItemAsString(ANONYMOUS_ID_KEY);
        if (Guid.TryParse(anonymousId, out _)) return anonymousId;

        anonymousId = Guid.NewGuid().ToString();
        _localStorage.SetItemAsString(ANONYMOUS_ID_KEY, anonymousId);
        return anonymousId;
    }
}
