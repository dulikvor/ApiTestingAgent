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
            
            // Get ChatConfiguration from bound options for conditional service registration
            var chatConfig = new ChatConfiguration();
            _configuration.GetSection(nameof(ServiceConfiguration.ChatConfiguration)).Bind(chatConfig);
            
            // Configure CORS based on chat type
            if (chatConfig.ChatType == "LocalChat")
            {
                services.AddCors(options =>
                {
                    options.AddDefaultPolicy(builder => builder
                        .WithOrigins("http://localhost:3001")
                        .AllowAnyMethod()
                        .AllowAnyHeader());
                });
            }
            
            services.AddControllers(options =>
            {
                options.Filters.Add(typeof(HttpContextCallContextFilter));
            });
            
            // Configure authentication based on chat type
            if (chatConfig.ChatType == "CopilotChat")
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = GitHubAuthenticationHandler.GitHubScheme;
                    options.DefaultChallengeScheme = GitHubAuthenticationHandler.GitHubScheme;
                })
                .AddScheme<GitHubAuthenticationSchemeOptions, GitHubAuthenticationHandler>(GitHubAuthenticationHandler.GitHubScheme, options => { });
            }
            else if (chatConfig.ChatType == "LocalChat")
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = LocalChatAuthenticationHandler.LocalChatScheme;
                    options.DefaultChallengeScheme = LocalChatAuthenticationHandler.LocalChatScheme;
                })
                .AddScheme<LocalChatAuthenticationSchemeOptions, LocalChatAuthenticationHandler>(LocalChatAuthenticationHandler.LocalChatScheme, options => 
                {
                    options.AllowedAppName = chatConfig.AllowedAppName;
                });
            }
            services.AddAgentAssets(_configuration);

            services.AddServiceHttpClient<IGitHubAuthenticationClient, GitHubAuthenticationClient, GitHubAuthenticationClientOptions>(GitHubAuthenticationClient.TokenCreator);
            services.AddSingleton<ITypedHttpServiceClientFactory, TypedHttpServiceClientFactory>();

            // Register stream writer based on chat type
            if (chatConfig.ChatType == "LocalChat")
            {
                services.AddSingleton<IResponseStreamWriter<LocalChatServerSentEventsStreamWriter>, LocalChatServerSentEventsStreamWriter>();
                services.AddSingleton<IStreamWriter, LocalChatServerSentEventsStreamWriter>();
            }
            else
            {
                services.AddSingleton<IResponseStreamWriter<CopilotServerSentEventsStreamWriter>, CopilotServerSentEventsStreamWriter>();
                services.AddSingleton<IStreamWriter, CopilotServerSentEventsStreamWriter>();
            }
            
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
            app.UseCors();
            app.UseRouting();
            app.UseAuthentication(); // Always use authentication middleware
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

            services.AddOptions<ChatConfiguration>()
                .Bind(_configuration.GetSection(nameof(ServiceConfiguration.ChatConfiguration)))
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }
    }
}
