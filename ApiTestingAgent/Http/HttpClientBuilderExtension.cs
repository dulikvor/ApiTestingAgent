using Microsoft.Extensions.DependencyInjection;

namespace ApiTestingAgent.Http
{
    public static class HttpClientBuilderExtension
    {
        public static IHttpClientBuilder HttpClientConfiguration(this IHttpClientBuilder httpClientBuilder)
        {
            return httpClientBuilder
                .SetHandlerLifetime(TimeSpan.FromHours(1));
        }
    }
}
