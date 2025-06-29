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
            var resolvedToken = await _tokenCreator(_options.Audience, _options.ApiKey);
            request.Headers.Authorization = new AuthenticationHeaderValue(resolvedToken.Schema, resolvedToken.Token);

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
