namespace ApiTestingAgent;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        await host.RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args);
        return builder
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
                webBuilder.UseUrls("http://+:5991");
            })
            .ConfigureAppConfiguration((context, config) =>
            {
                config.InitializeServiceConfiguration(context);
            });
    }
}