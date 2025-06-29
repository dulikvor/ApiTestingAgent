using ApiTestingAgent.Data;
using ApiTestingAgent.Http;
using System.Text;

namespace ApiTestingAgent.Tools.Utitlities
{
    public class GitHubRawContentCdnClient : IGitHubRawContentCdnClient
    {
        private readonly HttpClient _httpClient;

        public GitHubRawContentCdnClient(HttpClient httpClient) 
        {
            _httpClient = httpClient;
        }

        public async Task<string> GetRawContent(string user, string repo, string branch, string pathToFile)
        {
            ArgumentValidationHelper.Ensure.NotNull(user, "User");
            ArgumentValidationHelper.Ensure.NotNull(repo, "Repo");
            ArgumentValidationHelper.Ensure.NotNull(branch, "Branch");
            ArgumentValidationHelper.Ensure.NotNull(pathToFile, "pathToFile");

            var headers = new Dictionary<string, string>
            {
                { "User-Agent", "Argus" }
            };
            var stringBuilder = new StringBuilder()
                .Append(user)
                .Append("/")
                .Append(repo)
                .Append("/")
                .Append(branch)
                .Append("/")
                .Append(pathToFile);
            return await _httpClient.GetAsync<string>(stringBuilder.ToString(), headers);
        }
    }
}
