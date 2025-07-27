using System.Text.Json.Serialization;

namespace ApiTestingAgent.Resources.Schemas
{
    public class ExecutionSummary
    {
        [JsonPropertyName("totalSteps")]
        public int TotalSteps { get; set; }

        [JsonPropertyName("successfulSteps")]
        public int SuccessfulSteps { get; set; }

        [JsonPropertyName("failedStep")]
        public int? FailedStep { get; set; }

        [JsonPropertyName("stoppedExecution")]
        public bool StoppedExecution { get; set; }
    }

    public class CommandInvokeOutput
    {
        [JsonPropertyName("analysis")]
        public string? Analysis { get; set; }

        [JsonPropertyName("outcomeMatched")]
        public bool OutcomeMatched { get; set; }

        [JsonPropertyName("correctedUserMessage")]
        public string? CorrectedUserMessage { get; set; }

        [JsonPropertyName("stepNumber")]
        public int? StepNumber { get; set; }

        [JsonPropertyName("executionSummary")]
        public ExecutionSummary? ExecutionSummary { get; set; }

        [JsonPropertyName("nextState")]
        public string? NextState { get; set; }

        [JsonPropertyName("reasoning")]
        public string? Reasoning { get; set; }
    }
}
