namespace ApiTestingAgent.Data
{
    public class ServiceConfiguration
    {
        public GitHubAuthenticationClientOptions? GitHubAuthenticationClient { get; set; }
        public GitHubRawContentCdnClientOptions? GitHubRawContentCdnClient { get; set; }
        public ChatConfiguration? ChatConfiguration { get; set; }
        public FeaturesConfiguration Features { get; set; } = new FeaturesConfiguration();
    }
}
