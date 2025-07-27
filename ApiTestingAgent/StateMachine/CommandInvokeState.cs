using ApiTestingAgent.Agent;
using ApiTestingAgent.Contracts.SemanticKernel;
using ApiTestingAgent.Data;
using ApiTestingAgent.Data.Stream;
using ApiTestingAgent.Prompts;
using ApiTestingAgent.Resources.Schemas;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;

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
                // Use the JsonSerializerExtensions to clean and deserialize
                commandInvoke = JsonSerializerExtensions.DeserializeClean<CommandInvokeOutput>(chatMessageContent.Content!);
            }
            catch
            {
                Console.WriteLine("CommandInvoke (Raw):\n" + chatMessageContent.Content!+ "\n\n");
                // If deserialization fails, retry
                return (ApiTestStateTransitions.CommandInvocation, true);
            }

            Console.WriteLine("CommandInvoke (JSON):\n" + System.Text.Json.JsonSerializer.Serialize(commandInvoke, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n\n");

            // Handle corrected user message if present
            if (commandInvoke?.CorrectedUserMessage != null)
            {
                session.AddStepResult("CorrectedUserMessage", commandInvoke.CorrectedUserMessage);
            }

            // Handle state transitions based on nextState
            // Handle state transitions based on the response
            var (nextTransition, shouldTransition) = DetermineNextTransition(context, session, commandInvoke);
            if (shouldTransition)
            {
                return (nextTransition, true);
            }

            // Always report the analysis to the user
            Console.WriteLine($"ResponseToUser:\n{commandInvoke!.Analysis!}");
            await _streamReporter.ReportAsync(new List<ChatMessageContent> { chatMessageContent.CloneWithContent(commandInvoke!.Analysis!) });

            return (ApiTestStateTransitions.CommandInvocation, false);
        }

        private static ApiTestStateTransitions? ParseNextStateToTransition(string? nextState)
        {
            if (string.IsNullOrWhiteSpace(nextState))
                return null;

            return nextState.ToUpper() switch
            {
                "COMMANDSELECT" => ApiTestStateTransitions.CommandSelect,
                "EXECUTIONPLANSELECT" => ApiTestStateTransitions.ExecutionPlanSelect,
                "COMMANDINVOCATION" => ApiTestStateTransitions.CommandInvocation,
                "DOMAINSELECT" => ApiTestStateTransitions.DomainSelect,
                "RESTDISCOVERY" => ApiTestStateTransitions.RestDiscovery,
                "COMMANDINVOCATIONANALYSIS" => ApiTestStateTransitions.CommandInvocationAnalysis,
                _ => null
            };
        }

        private (ApiTestStateTransitions, bool) DetermineNextTransition(
            StateContext<ApiTestStateTransitions> context,
            Session<ApiTestStateTransitions> session,
            CommandInvokeOutput? commandInvoke)
        {
            // Only transition if nextState is not null or "None"
            if (string.IsNullOrEmpty(commandInvoke?.NextState) || 
                commandInvoke.NextState.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                return (ApiTestStateTransitions.CommandInvocation, false);
            }

            var nextTransition = ParseNextStateToTransition(commandInvoke.NextState);
            switch (nextTransition)
            {
                case ApiTestStateTransitions.CommandSelect:
                    var commandSelectState = _stateFactory.Create<CommandSelectState, ApiTestStateTransitions>();
                    context.SetState(commandSelectState);
                    session.SetCurrentStep(context.GetCurrentState(), ApiTestStateTransitions.CommandSelect);
                    return (ApiTestStateTransitions.CommandSelect, true);

                case ApiTestStateTransitions.ExecutionPlanSelect:
                    var executionPlanState = _stateFactory.Create<ExecutionPlanState, ApiTestStateTransitions>();
                    context.SetState(executionPlanState);
                    session.SetCurrentStep(context.GetCurrentState(), ApiTestStateTransitions.ExecutionPlanSelect);
                    return (ApiTestStateTransitions.ExecutionPlanSelect, true);
                case null:
                default: // Includes any unrecognized values
                    Console.WriteLine($"Unknown or unhandled NextState: {commandInvoke.NextState}");
                    return (ApiTestStateTransitions.CommandInvocation, false);
            }
        }
    }
}
