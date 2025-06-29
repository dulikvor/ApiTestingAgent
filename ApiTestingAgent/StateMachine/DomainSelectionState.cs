using ApiTestingAgent.Agent;
using ApiTestingAgent.Contracts.SemanticKernel;
using ApiTestingAgent.Data.Stream;
using ApiTestingAgent.Prompts;
using ApiTestingAgent.Resources.Schemas;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ApiTestingAgent.StateMachine
{
    public class DomainSelectionState : State<ApiTestStateTransitions>
    {
        public override string GetName() => nameof(DomainSelectionState);

        public DomainSelectionState(
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
            var prompt = await _promptAndSchemaRegistry.GetPrompt("DomainSelection");
            chatHistory.Add(new ChatMessageContent(AuthorRole.System, prompt));

            var chatMessagesContent = await _chatCompletionAgent.GetChatCompletionAsync(chatHistory);

            var messages = chatMessagesContent.ToList();
            if (messages.Count != 1)
                throw new InvalidOperationException($"Expected a single response message, got {messages.Count}.");
            var originalMessage = messages[0];
            DomainSelectionOutput? domainSelection = null;
            try
            {
                domainSelection = System.Text.Json.JsonSerializer.Deserialize<DomainSelectionOutput>(originalMessage.Content!);
            }
            catch
            {
                // If deserialization fails, retry
                return (ApiTestStateTransitions.DomainSelect, true);
            }

            // Print the isConfirmed flag to the console
            Console.WriteLine($"isConfirmed: {domainSelection?.IsConfirmed}");

            if(domainSelection?.DetectedDomain != null)
            {
                session.AddStepResult("SelectedDomain", domainSelection.DetectedDomain);
            }

            // If confirmed, switch to next state
            if (domainSelection?.IsConfirmed == true)
            {
                var nextState = _stateFactory.Create<RestDiscoveryState, ApiTestStateTransitions>();
                context.SetState(nextState);
                session.SetCurrentStep(context.GetCurrentState(), ApiTestStateTransitions.RestDiscovery);
                return (ApiTestStateTransitions.RestDiscovery, true);
            }

            await _streamReporter.ReportAsync(new List<ChatMessageContent> { originalMessage.CloneWithContent(domainSelection!.UserResponse!) });

            return (ApiTestStateTransitions.DomainSelect, false);
        }
    }
}
