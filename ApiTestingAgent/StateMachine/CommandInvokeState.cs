using ApiTestingAgent.Agent;
using ApiTestingAgent.Contracts.SemanticKernel;
using ApiTestingAgent.Data.Stream;
using ApiTestingAgent.Prompts;
using ApiTestingAgent.Resources.Schemas;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ApiTestingAgent.StateMachine
{
    public class CommandInvokeState : State<ApiTestStateTransitions>
    {
        public override string GetName() => nameof(CommandInvokeState);

        public CommandInvokeState(
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
            var prompt = await _promptAndSchemaRegistry.GetPrompt("CommandInvoke");
            chatHistory.Add(new ChatMessageContent(AuthorRole.System, prompt));

            var chatMessageContent = await _chatCompletionAgent.PlanInvokeAsync(chatHistory, CancellationToken.None);
            // Assume the planner result is a JSON string matching CommandInvokeOutput
            CommandInvokeOutput? commandInvoke = null;
            try
            {
                commandInvoke = System.Text.Json.JsonSerializer.Deserialize<CommandInvokeOutput>(chatMessageContent.Content!);
            }
            catch
            {
                // If deserialization fails, retry
                return (ApiTestStateTransitions.CommandInvocation, true);
            }

            Console.WriteLine("CommandInvoke (JSON):\n" + System.Text.Json.JsonSerializer.Serialize(commandInvoke, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n\n");

            if (commandInvoke?.NextState?.Equals(
                ApiTestStateTransitions.CommandSelect.ToString(), StringComparison.OrdinalIgnoreCase) == true)
            {
                var nextState = _stateFactory.Create<CommandSelectState, ApiTestStateTransitions>();
                context.SetState(nextState);
                session.SetCurrentStep(context.GetCurrentState(), ApiTestStateTransitions.CommandSelect);
                return (ApiTestStateTransitions.CommandSelect, true);
            }

            if (commandInvoke?.CorrectedUserMessage != null)
            {
                session.AddStepResult("CorrectedUserMessage", commandInvoke.CorrectedUserMessage);
            }

            Console.WriteLine($"ResponseToUser:\n{commandInvoke!.Analysis!}");
            await _streamReporter.ReportAsync(new List<ChatMessageContent> { chatMessageContent.CloneWithContent(commandInvoke!.Analysis!) });

            return (ApiTestStateTransitions.CommandInvocation, false); // Or another transition as appropriate
        }
    }
}
