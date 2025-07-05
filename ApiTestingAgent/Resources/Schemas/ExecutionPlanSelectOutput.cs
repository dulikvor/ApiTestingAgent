using System.Text.Json.Serialization;

namespace ApiTestingAgent.Resources.Schemas
{
    public class ExecutionPlanStep
    {
        [JsonPropertyName("stepNumber")]
        public int StepNumber { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public object? Body { get; set; }

        [JsonPropertyName("expectation")]
        public ExecutionExpectation? Expectation { get; set; }
    }

    public class ExecutionExpectation
    {
        [JsonPropertyName("expectedStatusCode")]
        public int ExpectedStatusCode { get; set; }

        [JsonPropertyName("expectedContent")]
        public object? ExpectedContent { get; set; }
    }

    public class ExecutionPlanChange
    {
        [JsonPropertyName("changeType")]
        public string ChangeType { get; set; } = string.Empty;

        [JsonPropertyName("stepNumber")]
        public int StepNumber { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }

    public class ExecutionPlanSelectOutput
    {
        [JsonPropertyName("updatedPlan")]
        public List<ExecutionPlanStep> UpdatedPlan { get; set; } = new();

        [JsonPropertyName("changes")]
        public List<ExecutionPlanChange> Changes { get; set; } = new();

        [JsonPropertyName("isConfirmed")]
        public bool IsConfirmed { get; set; }

        [JsonPropertyName("userResponse")]
        public string UserResponse { get; set; } = string.Empty;
    }
}
