namespace ApiTestingAgent.Data
{
    public class ServiceConfiguration
    {
        public GitHubAuthenticationClientOptions? GitHubAuthenticationClient { get; set; }
        public GitHubRawContentCdnClientOptions? GitHubRawContentCdnClient { get; set; }
    }
}
