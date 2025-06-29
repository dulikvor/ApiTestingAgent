using System.Text.Json.Serialization;

namespace ApiTestingAgent.Resources.Schemas
{
    public class CommandInvokeOutput
    {
        [JsonPropertyName("analysis")]
        public string? Analysis { get; set; }

        [JsonPropertyName("outcomeMatched")]
        public bool OutcomeMatched { get; set; }

        [JsonPropertyName("correctedUserMessage")]
        public string? CorrectedUserMessage { get; set; }

        [JsonPropertyName("nextState")]
        public string? NextState { get; set; }

        [JsonPropertyName("reasoning")]
        public string? Reasoning { get; set; }
    }
}
