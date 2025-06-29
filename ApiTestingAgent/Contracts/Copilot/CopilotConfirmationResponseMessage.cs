using System.Text.Json.Serialization;
using ApiTestingAgent.Contracts;

namespace ApiTestingAgent.Contracts.Copilot
{
    public enum ConfirmationState
    {
        Accepted,
        Dismissed
    }

    public class CopilotConfirmationResponseMessage
    {
        [JsonPropertyName("state")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ConfirmationState State { get; set; } // The state of the confirmation: Accepted or Dismissed.

        [JsonPropertyName("confirmation")]
        public ConfirmationData? Confirmation { get; set; } // Data identifying the relevant action.
    }
}
