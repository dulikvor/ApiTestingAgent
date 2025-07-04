using System.Text.Json.Serialization;

namespace ApiTestingAgent.Resources.Schemas;
public class DomainSelectionOutput
{
    [JsonPropertyName("detectedDomain")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? DetectedDomain { get; set; }

    [JsonPropertyName("userResponse")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? UserResponse { get; set; }

    [JsonPropertyName("isConfirmed")]
    public bool IsConfirmed { get; set; }
}