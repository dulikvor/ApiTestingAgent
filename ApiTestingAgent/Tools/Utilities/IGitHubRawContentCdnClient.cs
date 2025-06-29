namespace ApiTestingAgent.Tools.Utitlities
{
    public interface IGitHubRawContentCdnClient
    {
        public Task<string> GetRawContent(string user, string repo, string branch, string pathToFile);
    }
}