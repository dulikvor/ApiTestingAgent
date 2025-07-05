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

            // Ensure the context key exists before any tool call that will update it
            var existingJson = ApiTestingAgent.Data.CallContext.GetData("SwaggerOperationsKey") as string;
            if (string.IsNullOrEmpty(existingJson))
            {
                ApiTestingAgent.Data.CallContext.SetData("SwaggerOperationsKey", "[]");
                Console.WriteLine("SwaggerOperationsKey initialized as empty array in context (from RestDiscoveryState, before tool call).");
            }
            var chatMessageContent = await _chatCompletionAgent.PlanInvokeAsync(chatHistory, CancellationToken.None);
            // Print the content of chatMessageContent
            Console.WriteLine($"chatMessageContent.Content: {chatMessageContent.Content}");
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

            if (restDiscovery?.DetectedOperations?.Any() == true)
            {
                var (operationsString, operationsWithContentString) = FormatDetectedOperations(restDiscovery.DetectedOperations);
                session.AddStepResult("DetectedRestOperations", operationsString);
                session.AddStepResult("DetectedRestOperationsWithContent", operationsWithContentString);
            }

            // Handle detectedSwaggerRoutes if present
            if (restDiscovery?.DetectedSwaggerRoutes?.Any() == true)
            {
                var routesString = FormatDetectedSwaggerRoutes(restDiscovery.DetectedSwaggerRoutes);
                session.AddStepResult("DetectedSwaggerRoutes", routesString);
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
            // Ignore detectedOperations, use the full operations list from the logical context
            var operationsJson = ApiTestingAgent.Data.GlobalContext.GetData("SwaggerOperationsKey") as string;
            // Debug: print the JSON retrieved from the context
            Console.WriteLine("SwaggerOperationsKey from context: " + (operationsJson ?? "null"));
            List<ApiTestingAgent.Tools.Utitlities.SwaggerOperation>? fullOperations = null;
            if (!string.IsNullOrEmpty(operationsJson))
            {
                try
                {
                    fullOperations = System.Text.Json.JsonSerializer.Deserialize<List<ApiTestingAgent.Tools.Utitlities.SwaggerOperation>>(operationsJson);
                    // Debug: print the deserialized operations
                    Console.WriteLine("Deserialized operations from context:");
                    foreach (var op in fullOperations!)
                    {
                        Console.WriteLine($"  Method: {op.HttpMethod}, Url: {op.Url}, Content: {(op.Content != null ? op.Content.ToJsonString() : "null")}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to deserialize SwaggerOperationsKey: " + ex);
                }
            }
            var opsStripped = new List<string>();
            var opsWithContent = new List<string>();
            if (fullOperations != null)
            {
                foreach (var op in fullOperations)
                {
                    var method = op.HttpMethod;
                    var path = op.Url;
                    var apiVersion = op.ApiVersion;
                    var content = op.Content?.ToJsonString();
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
            }
            var result = (string.Join("\n", opsStripped), string.Join("\n", opsWithContent));
            // Print the returned operations with content
            Console.WriteLine("Returned operations with content:\n" + result.Item2);
            return result;
        }

        private static string FormatDetectedSwaggerRoutes(Dictionary<string, string> detectedSwaggerRoutes)
        {
            var lines = new List<string>();
            foreach (var kvp in detectedSwaggerRoutes)
            {
                lines.Add($"API Version: {kvp.Key}, Route: {kvp.Value}");
            }
            return string.Join("\n", lines);
        }
    }
}
