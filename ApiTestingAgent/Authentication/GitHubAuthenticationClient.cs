using ApiTestingAgent.Contracts;
using ApiTestingAgent.Contracts.Github;
using ApiTestingAgent.Data;
using ApiTestingAgent.Http;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace ApiTestingAgent.Authentication
{
    public class GitHubAuthenticationClient : IGitHubAuthenticationClient
    {
        private readonly HttpClient _httpClient;

        public GitHubAuthenticationClient(HttpClient httpClient) 
        {
            _httpClient = httpClient;
        }

        public static Task<(string Schema, string Token)> TokenCreator(string scope, object param)
        {
            var token = CallContext.GetData("GitHubTokenKey") as string;
            return Task.FromResult((JwtBearerDefaults.AuthenticationScheme, token!));
        }

        public async Task<GitHubAuthenticationContract> GetUserAsync()
        {
            var headers = new Dictionary<string, string>
            {
                { "User-Agent", "ApiTestingAgent" }
            };
            return await _httpClient.GetAsync<GitHubAuthenticationContract>("user", headers);
        }
    }
}
