using ApiTestingAgent.Contracts;
using ApiTestingAgent.Contracts.Github;

namespace ApiTestingAgent.Authentication
{
    public interface IGitHubAuthenticationClient
    {
        public Task<GitHubAuthenticationContract> GetUserAsync();
    }
}
