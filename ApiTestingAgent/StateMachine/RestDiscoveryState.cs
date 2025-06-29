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

            Console.WriteLine("RestDiscovery (JSON):\n" + System.Text.Json.JsonSerializer.Serialize(restDiscovery, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n\n");

            if (restDiscovery?.RawSwaggerContent != null)
            {
                session.AddStepResult("RawSwaggerContent", restDiscovery.RawSwaggerContent);
            }

            if (restDiscovery?.DetectedOperations?.Any() == true)
            {
                var (operationsString, operationsWithContentString) = FormatDetectedOperations(restDiscovery.DetectedOperations);
                session.AddStepResult("DetectedRestOperations", operationsString);
                session.AddStepResult("DetectedRestOperationsWithContent", operationsWithContentString);
            }

            if (restDiscovery?.IsConfirmed == true && session.StepResultExists("DetectedRestOperations"))
            {
                var nextState = _stateFactory.Create<CommandInvokeState, ApiTestStateTransitions>();
                context.SetState(nextState);
                session.SetCurrentStep(context.GetCurrentState(), ApiTestStateTransitions.CommandSelect);
                return (ApiTestStateTransitions.CommandInvocation, true);
            }

            Console.WriteLine($"ResponseToUser:\n{restDiscovery!.UserResponse!}");
            await _streamReporter.ReportAsync(new List<ChatMessageContent> { chatMessageContent.CloneWithContent(restDiscovery!.UserResponse!) });

            return (ApiTestStateTransitions.RestDiscovery, false); // Or another transition as appropriate
        }

        private static (string, string) FormatDetectedOperations(List<object> detectedOperations)
        {
            var opsStripped = new List<string>();
            var opsWithContent = new List<string>();
            foreach (var op in detectedOperations)
            {
                string? method = null;
                string? path = null;
                string? content = null;
                string? apiVersion = null;
                if (op is System.Text.Json.JsonElement elem && elem.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (elem.TryGetProperty("method", out var methodProp))
                        method = methodProp.GetString();
                    else if (elem.TryGetProperty("HttpMethod", out var methodProp2))
                        method = methodProp2.GetString();
                    if (elem.TryGetProperty("path", out var pathProp))
                        path = pathProp.GetString();
                    else if (elem.TryGetProperty("Url", out var urlProp))
                        path = urlProp.GetString();
                    if (elem.TryGetProperty("Content", out var contentProp))
                        content = contentProp.ToString();
                    if (elem.TryGetProperty("ApiVersion", out var apiVersionProp))
                        apiVersion = apiVersionProp.GetString();
                    // Add api-version as query param if available and not already present
                    if (!string.IsNullOrEmpty(apiVersion) && !string.IsNullOrEmpty(path))
                    {
                        var separator = path.Contains("?") ? "&" : "?";
                        if (!path.Contains("api-version="))
                            path += $"{separator}api-version={apiVersion}";
                    }
                    opsStripped.Add($"Operation method: {method}, path: {path}");
                    opsWithContent.Add(content != null ? $"Operation method: {method}, path: {path}, content: {content}" : $"Operation method: {method}, path: {path}");
                }
                else if (op is System.Collections.Generic.Dictionary<string, object> dict)
                {
                    dict.TryGetValue("method", out var methodObj);
                    dict.TryGetValue("path", out var pathObj);
                    if (methodObj == null && dict.TryGetValue("HttpMethod", out var methodObj2))
                        methodObj = methodObj2;
                    if (pathObj == null && dict.TryGetValue("Url", out var pathObj2))
                        pathObj = pathObj2;
                    if (dict.TryGetValue("Content", out var contentObj))
                        content = contentObj?.ToString();
                    if (dict.TryGetValue("ApiVersion", out var apiVersionObj))
                        apiVersion = apiVersionObj?.ToString();
                    // Add api-version as query param if available and not already present
                    if (!string.IsNullOrEmpty(apiVersion) && pathObj is string pathStr && !string.IsNullOrEmpty(pathStr))
                    {
                        var separator = pathStr.Contains("?") ? "&" : "?";
                        if (!pathStr.Contains("api-version="))
                            pathObj = pathStr + $"{separator}api-version={apiVersion}";
                    }
                    opsStripped.Add($"Operation method: {methodObj}, path: {pathObj}");
                    opsWithContent.Add(content != null ? $"Operation method: {methodObj}, path: {pathObj}, content: {content}" : $"Operation method: {methodObj}, path: {pathObj}");
                }
                else
                {
                    opsStripped.Add(op?.ToString() ?? string.Empty);
                    opsWithContent.Add(op?.ToString() ?? string.Empty);
                }
            }
            return (string.Join("\n", opsStripped), string.Join("\n", opsWithContent));
        }
    }
}
