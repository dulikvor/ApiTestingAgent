using System.Text.Json.Serialization;

namespace ApiTestingAgent.Resources.Schemas;

public class CommandSelectOutput
{

    [JsonPropertyName("isConfirmed")]
    public bool IsConfirmed { get; set; }

    [JsonPropertyName("commandIsValid")]
    public bool CommandIsValid { get; set; }

    [JsonPropertyName("userResponse")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? UserResponse { get; set; }

    [JsonPropertyName("httpMethod")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? HttpMethod { get; set; }

    [JsonPropertyName("requestUri")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? RequestUri { get; set; }

    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Content { get; set; }

    [JsonPropertyName("commandSelected")]
    public bool CommandSelected { get; set; }
    
    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; set; }
}