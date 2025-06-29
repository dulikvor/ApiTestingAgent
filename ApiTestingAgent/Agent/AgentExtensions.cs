using ApiTestingAgent.Data;
using ApiTestingAgent.Http;
using ApiTestingAgent.Prompts;
using ApiTestingAgent.Tools;
using ApiTestingAgent.Tools.Utitlities;
using Microsoft.SemanticKernel;

namespace ApiTestingAgent.Agent
{
    public static class AgentExtensions
    {
        public static IServiceCollection AddAgentAssets(this IServiceCollection services, IConfiguration configuration)
        {
            var builder = Kernel.CreateBuilder();
            builder.AddAzureOpenAIChatCompletion(
                configuration["AzureOpenAI:Deployment"] ?? throw new ArgumentNullException("AzureOpenAI:Deployment"),
                configuration["AzureOpenAI:Endpoint"] ?? throw new ArgumentNullException("AzureOpenAI:Endpoint"),
                configuration["AzureOpenAI:ApiKey"] ?? throw new ArgumentNullException("AzureOpenAI:ApiKey")
            );

            builder.Services.AddServiceHttpClient<IGitHubRawContentCdnClient, GitHubRawContentCdnClient, GitHubRawContentCdnClientOptions>();

            builder.Services.AddOptions<GitHubRawContentCdnClientOptions>()
            .Bind(configuration.GetSection(nameof(ServiceConfiguration.GitHubRawContentCdnClient)))
            .ValidateDataAnnotations()
            .ValidateOnStart();

            builder.Plugins.AddFromType<SwaggerTools>("SwaggerTool");

            var kernel = builder.Build();
            services.AddChatCompletionAgent(configuration, kernel);
            services.AddPromptsAndSchemas(configuration, kernel);
            return services;

        }

        private static IServiceCollection AddChatCompletionAgent(this IServiceCollection services, IConfiguration configuration, Kernel kernel)
        {
            // Create and initialize ChatCompletionAgent
            var chatCompletionAgent = new ChatCompletionAgent();
            chatCompletionAgent.Initialize(kernel);
            // Register the kernel and agent as singletons for DI
            services.AddSingleton<IChatCompletionAgent>(chatCompletionAgent);
            return services;
        }

        private static IServiceCollection AddPromptsAndSchemas(this IServiceCollection services, IConfiguration configuration, Kernel kernel)
        {
            // You may want to make these configurable
            var promptDirs = new[] { "Resources/Prompts" };
            var schemaDirs = new[] { "Resources/Schemas" };
            var promptAndSchemaRegistry = new PromptAndSchemaRegistry(kernel, promptDirs, schemaDirs);
            services.AddSingleton<IPromptAndSchemaRegistry>(promptAndSchemaRegistry);
            return services;
        }
    }
}
