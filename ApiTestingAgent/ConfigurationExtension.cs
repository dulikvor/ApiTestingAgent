using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ApiTestingAgent
{
    public static class ConfigurationExtension
    {
        public static void InitializeServiceConfiguration(this IConfigurationBuilder config, HostBuilderContext context)
        {
            var env = context.HostingEnvironment.EnvironmentName;
            config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true)
                .AddUserSecrets<Program>()
                .AddEnvironmentVariables();
        }
    }
}
