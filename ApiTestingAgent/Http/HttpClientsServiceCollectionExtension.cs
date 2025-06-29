using ApiTestingAgent.Data;
using ApiTestingAgent.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApiTestingAgent.Http
{
    public static class HttpClientsServiceCollectionExtension
    {
        public static IServiceCollection AddServiceHttpClient<TIClient, TClientImplementation, TEndpoint>(this IServiceCollection services, Aliases.TokenCreator? tokenCreator = null, DelegatingHandler? authenticationHandler = null)
            where TIClient : class
            where TClientImplementation : class, TIClient
            where TEndpoint : ServiceHttpClientOptions, new()
        {
            var httpClientBuilder = services.AddHttpClient<TIClient, TClientImplementation>(
                typeof(TIClient).Name,
                (provider, client) =>
                {
                    var options = provider.GetRequiredService<IOptions<TEndpoint>>().Value;
                    client.BaseAddress = options.Endpoint;
                })
                .HttpClientConfiguration();

            if (tokenCreator != null)
            {
                httpClientBuilder.AddHttpMessageHandler(provider =>
                {
                    var options = provider.GetRequiredService<IOptions<TEndpoint>>().Value;
                    return new HttpClientAuthenticationHandler(options, tokenCreator);
                });
            }



            return services;
        }

        public static IServiceCollection AddServiceHttpClient<TIClient, TClientImplementation>(this IServiceCollection services, Aliases.TokenCreator? tokenCreator = null, bool ignoreServerCertificateValidation = false)
            where TIClient : class
            where TClientImplementation : class, TIClient
        {
            var httpClientBuilder = services.AddHttpClient<TIClient, TClientImplementation>(
                typeof(TIClient).Name)
                .HttpClientConfiguration();

            if (ignoreServerCertificateValidation)
            {
                httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() =>
                    new System.Net.Http.HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                    });
            }

            if (tokenCreator != null)
            {
                httpClientBuilder.AddHttpMessageHandler(provider => new HttpClientAuthenticationHandler(default!, tokenCreator));
            }

            return services;
        }
    }
}
