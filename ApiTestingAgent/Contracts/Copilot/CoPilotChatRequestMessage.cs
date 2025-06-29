using System.Text.Json.Serialization;

namespace ApiTestingAgent.Contracts.Copilot
{
    public class CoPilotChatRequestMessage
    {
        [JsonPropertyName("model")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? Model { get; set; }

        [JsonPropertyName("messages")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public List<CopilotChatMessage>? Messages { get; set; }
    }
}
