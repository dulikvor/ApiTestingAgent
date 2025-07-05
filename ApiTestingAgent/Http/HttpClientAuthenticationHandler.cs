using System.Net.Http.Headers;
using ApiTestingAgent.Data;
using ApiTestingAgent.Http;

namespace ApiTestingAgent.Http
{
    public class HttpClientAuthenticationHandler : DelegatingHandler
    {
        Aliases.TokenCreator _tokenCreator;
        private readonly ServiceHttpClientOptions _options;

        public HttpClientAuthenticationHandler(ServiceHttpClientOptions options, Aliases.TokenCreator tokenCreator)
        {
            _tokenCreator = tokenCreator;
            _options = options;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Ensure _options.Audience and _options.ApiKey are not null
            var audience = _options.Audience ?? string.Empty;
            var apiKey = _options.ApiKey ?? string.Empty;
            var resolvedToken = await _tokenCreator(audience, apiKey);
            request.Headers.Authorization = new AuthenticationHeaderValue(resolvedToken.Schema, resolvedToken.Token);

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
