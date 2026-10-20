using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace WebSpa.Services;

/// <summary>
/// Adds the JWT (if logged in) and the anonymous basket id to every API call.
/// </summary>
public class ApiAuthHandler : DelegatingHandler
{
    public const string ANONYMOUS_ID_HEADER = "X-Anonymous-Id";

    private readonly AuthTokenStore _tokenStore;

    public ApiAuthHandler(AuthTokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = _tokenStore.Token;
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        request.Headers.TryAddWithoutValidation(ANONYMOUS_ID_HEADER, _tokenStore.GetOrCreateAnonymousId());

        return base.SendAsync(request, cancellationToken);
    }
}
