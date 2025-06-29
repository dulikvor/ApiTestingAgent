using ApiTestingAgent.Agent;
using ApiTestingAgent.Authentication;
using ApiTestingAgent.Data;
using ApiTestingAgent.Data.Stream;
using ApiTestingAgent.Http;
using ApiTestingAgent.StateMachine;
using ApiTestingAgent.Services;


namespace ApiTestingAgent
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            ConfigurationBinding(services);
            services.AddControllers(options =>
            {
                options.Filters.Add(typeof(HttpContextCallContextFilter));
            });
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = GitHubAuthenticationHandler.GitHubScheme;
                options.DefaultChallengeScheme = GitHubAuthenticationHandler.GitHubScheme;
            })
            .AddScheme<GitHubAuthenticationSchemeOptions, GitHubAuthenticationHandler>(GitHubAuthenticationHandler.GitHubScheme, options => { });
            services.AddAgentAssets(_configuration);

            services.AddServiceHttpClient<IGitHubAuthenticationClient, GitHubAuthenticationClient, GitHubAuthenticationClientOptions>(GitHubAuthenticationClient.TokenCreator);
            services.AddSingleton<ITypedHttpServiceClientFactory, TypedHttpServiceClientFactory>();

            // Register CopilotServerSentEventsStreamWriter for IResponseStreamWriter<>
            services.AddSingleton<IResponseStreamWriter<CopilotServerSentEventsStreamWriter>, CopilotServerSentEventsStreamWriter>();
            // Register StreamReporter for DI
            services.AddSingleton<StreamReporter>();
            // Register IStateFactory for DI
            services.AddSingleton<IStateFactory, StateFactory>();
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            services.AddSingleton<IApiTestService, ApiTestService>();
            services.AddSingleton<DomainSelectionState>();
            services.AddSingleton<RestDiscoveryState>();
            services.AddSingleton<CommandSelectState>();
            services.AddSingleton<CommandInvokeState>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthentication(); // Ensure authentication middleware is called before authorization
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private void ConfigurationBinding(IServiceCollection services)
    {
        services.AddOptions<GitHubAuthenticationClientOptions>()
            .Bind(_configuration.GetSection(nameof(ServiceConfiguration.GitHubAuthenticationClient)))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
    }
}
