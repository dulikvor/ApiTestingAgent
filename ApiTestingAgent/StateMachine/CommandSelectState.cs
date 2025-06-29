using ApiTestingAgent.Agent;
using ApiTestingAgent.Contracts.SemanticKernel;
using ApiTestingAgent.Data.Stream;
using ApiTestingAgent.Prompts;
using ApiTestingAgent.Resources.Schemas;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ApiTestingAgent.StateMachine
{
    public class CommandSelectState : State<ApiTestStateTransitions>
    {
        public override string GetName() => nameof(CommandSelectState);

        public CommandSelectState(
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

            var detectedRestOperations = new Dictionary<string, string>
            {
                ["DetectedRestOperationsWithContent"] = session.StepResult.TryGetValue("DetectedRestOperationsWithContent", out var selectedDomain) ? selectedDomain?.ToString() ?? "none" : "none",
            };
            chatHistory.RemoveSystemMessagesContaining("Detected Commands With Content:");
            var swaggerDefinitionContextPrompt = await _promptAndSchemaRegistry.GetPrompt("SwaggerDefinition", detectedRestOperations);
            chatHistory.Add(new ChatMessageContent(AuthorRole.System, swaggerDefinitionContextPrompt));

            var prompt = await _promptAndSchemaRegistry.GetPrompt("CommandSelect");
            chatHistory.Add(new ChatMessageContent(AuthorRole.System, prompt));

            var chatMessagesContent = await _chatCompletionAgent.GetChatCompletionAsync(chatHistory);

            var messages = chatMessagesContent.ToList();
            if (messages.Count != 1)
                throw new InvalidOperationException($"Expected a single response message, got {messages.Count}.");
            var originalMessage = messages[0];
            CommandSelectOutput? commandSelect = null;
            try
            {
                commandSelect = System.Text.Json.JsonSerializer.Deserialize<CommandSelectOutput>(originalMessage.Content!);
            }
            catch
            {
                // If deserialization fails, retry
                return (ApiTestStateTransitions.CommandSelect, true);
            }
            
            Console.WriteLine("CommandSelect (JSON):\n" + System.Text.Json.JsonSerializer.Serialize(commandSelect, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n\n");

            if (commandSelect?.CommandSelected != null)
            {
                var formatted = FormatSelectedCommand(commandSelect);
                session.AddStepResult("SelectedCommand", formatted);

                session.RemoveStepResult("CorrectedUserMessage");
            }

            if (commandSelect!.CommandIsValid == true && commandSelect.IsConfirmed)
            {
                var nextState = _stateFactory.Create<CommandInvokeState, ApiTestStateTransitions>();
                context.SetState(nextState);
                session.SetCurrentStep(context.GetCurrentState(), ApiTestStateTransitions.CommandSelect);
                return (ApiTestStateTransitions.CommandInvocation, true);
            }

            Console.WriteLine($"ResponseToUser:\n{commandSelect!.UserResponse!}");
            await _streamReporter.ReportAsync(new List<ChatMessageContent> { originalMessage.CloneWithContent(commandSelect!.UserResponse!) });

            return (ApiTestStateTransitions.CommandSelect, false);
        }

        private string FormatSelectedCommand(CommandSelectOutput command)
        {
            var method = command.HttpMethod ?? string.Empty;
            var uri = command.RequestUri ?? string.Empty;
            var content = command.Content ?? "{}";
            return $"Http Method: {method}\nRequest Uri: {uri}\nRequest Content:```json\n{content}\n```";
        }
    }
}
