using ApiTestingAgent.Agent;
using ApiTestingAgent.Contracts.SemanticKernel;
using ApiTestingAgent.Data.Stream;
using ApiTestingAgent.Prompts;
using ApiTestingAgent.Resources.Schemas;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ApiTestingAgent.StateMachine
{
    public class RestDiscoveryState : State<ApiTestStateTransitions>
    {
        public override string GetName() => nameof(RestDiscoveryState);

        public RestDiscoveryState(
            ILogger<State<ApiTestStateTransitions>> logger,
            StreamReporter streamReporter,
            IPromptAndSchemaRegistry promptAndSchemaRegistry,
            IStateFactory stateFactory,
            IChatCompletionAgent chatCompletionAgent)
            : base(logger, streamReporter, promptAndSchemaRegistry, stateFactory, chatCompletionAgent)
        {
        }

        public override async Task<(ApiTestStateTransitions, bool)> HandleState(
            StateContext<ApiTestStateTransitions> context,
            Session<ApiTestStateTransitions> session,
            ApiTestStateTransitions transition,
            ChatHistory chatHistory)
        {
            var prompt = await _promptAndSchemaRegistry.GetPrompt("RestDiscovery");
            chatHistory.Add(new ChatMessageContent(AuthorRole.System, prompt));

            var chatMessageContent = await _chatCompletionAgent.PlanInvokeAsync(chatHistory, CancellationToken.None);
            // Assume the planner result is a JSON string matching RestDiscoveryOutput
            RestDiscoveryOutput? restDiscovery = null;
            try
            {
                restDiscovery = System.Text.Json.JsonSerializer.Deserialize<RestDiscoveryOutput>(chatMessageContent.Content!);
            }
            catch
            {
                // If deserialization fails, retry
                return (ApiTestStateTransitions.RestDiscovery, true);
            }

            if (restDiscovery?.RawSwaggerContent != null)
            {
                session.AddStepResult("RawSwaggerContent", restDiscovery.RawSwaggerContent);
            }

            if (restDiscovery?.DetectedOperations?.Any() == true)
            {
                var operationsString = FormatDetectedOperations(restDiscovery.DetectedOperations);
                session.AddStepResult("DetectedRestOperations", operationsString);
            }

            await _streamReporter.ReportAsync(new List<ChatMessageContent> { chatMessageContent.CloneWithContent(restDiscovery!.UserResponse!) });

            return (ApiTestStateTransitions.RestDiscovery, false); // Or another transition as appropriate
        }

        private static string FormatDetectedOperations(List<object> detectedOperations)
        {
            return string.Join("\n", detectedOperations.Select(op =>
            {
                if (op is System.Text.Json.JsonElement elem && elem.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    string? method = null;
                    string? path = null;
                    // Try both property name variants
                    if (elem.TryGetProperty("method", out var methodProp))
                        method = methodProp.GetString();
                    else if (elem.TryGetProperty("HttpMethod", out var methodProp2))
                        method = methodProp2.GetString();
                    if (elem.TryGetProperty("path", out var pathProp))
                        path = pathProp.GetString();
                    else if (elem.TryGetProperty("Url", out var urlProp))
                        path = urlProp.GetString();
                    return $"Operation method: {method}, path: {path}";
                }
                else if (op is System.Collections.Generic.Dictionary<string, object> dict)
                {
                    dict.TryGetValue("method", out var methodObj);
                    dict.TryGetValue("path", out var pathObj);
                    if (methodObj == null && dict.TryGetValue("HttpMethod", out var methodObj2))
                        methodObj = methodObj2;
                    if (pathObj == null && dict.TryGetValue("Url", out var pathObj2))
                        pathObj = pathObj2;
                    return $"Operation method: {methodObj}, path: {pathObj}";
                }
                else
                {
                    return op?.ToString() ?? string.Empty;
                }
            }));
        }
    }
}
