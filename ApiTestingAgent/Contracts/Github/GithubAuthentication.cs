using System.Text.Json.Serialization;

namespace ApiTestingAgent.Contracts.Github
{
    public class GitHubAuthenticationContract
    {
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Name { get; set; }

        [JsonPropertyName("type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Type { get; set; }
    }
}