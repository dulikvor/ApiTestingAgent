using System.Text.Json.Serialization;

namespace ApiTestingAgent.Resources.Schemas
{
    public class RestDiscoveryOutput
    {
        [JsonPropertyName("userResponse")]
        public string? UserResponse { get; set; }

        [JsonPropertyName("rawSwaggerContent")]
        public string? RawSwaggerContent { get; set; }

        [JsonPropertyName("detectedOperations")]
        public List<object>? DetectedOperations { get; set; }

        [JsonPropertyName("isConfirmed")]
        public bool IsConfirmed { get; set; }
    }
}
