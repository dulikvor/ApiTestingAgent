using ApiTestingAgent.Agent;
using ApiTestingAgent.Contracts.Copilot;
using ApiTestingAgent.Data;
using ApiTestingAgent.Data.Stream;
using ApiTestingAgent.Prompts;
using ApiTestingAgent.StateMachine;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ApiTestingAgent.Services
{
    public class ApiTestService : IApiTestService
    {
        private readonly ILogger<State<ApiTestStateTransitions>> _logger;
        private readonly StreamReporter _streamReporter;
        private readonly IStateFactory _stateFactory;
        private readonly IPromptAndSchemaRegistry _promptAndSchemaRegistry;

        public ApiTestService(
            ILogger<State<ApiTestStateTransitions>> logger,
            StreamReporter streamReporter,
            IStateFactory stateFactory,
            IPromptAndSchemaRegistry promptAndSchemaRegistry)
        {
            _logger = logger;
            _streamReporter = streamReporter;
            _stateFactory = stateFactory;
            _promptAndSchemaRegistry = promptAndSchemaRegistry;
        }

        public async Task InvokeNext(HttpContext httpContext, CoPilotChatRequestMessage coPilotChatRequestMessage)
        {
            
            var session = SessionStore<Session<ApiTestStateTransitions>, ApiTestStateTransitions>.GetSessions((string)CallContext.GetData("UserNameKey")!);

            ApiTestStateTransitions transition = default;
            StateContext<ApiTestStateTransitions> stateContext;
            if (session.CurrentStep != null)
            {
                stateContext = new StateContext<ApiTestStateTransitions>(session.CurrentStep);
                transition = session.CurrentTransition;
            }
            else
            {
                stateContext = new StateContext<ApiTestStateTransitions>(_stateFactory.Create<DomainSelectionState, ApiTestStateTransitions>());
                transition = ApiTestStateTransitions.DomainSelect;
            }

            var chatHistory = new ChatHistory();
            chatHistory.AddCoPilotChatRequestMessages(coPilotChatRequestMessage);
            bool shouldProceed = false;
            do
            {
                // Collect current user selections from session StepResult
                var userSelections = new Dictionary<string, string>
                {
                    ["SelectedDomain"] = session.StepResult.TryGetValue("SelectedDomain", out var selectedDomain) ? selectedDomain ?? "none" : "none",
                    ["DetectedRestOperations"] = session.StepResult.TryGetValue("DetectedRestOperations", out var detectedRestOps) ? detectedRestOps ?? "none" : "none",
                    ["SelectedCommand"] = session.StepResult.TryGetValue("SelectedCommand", out var selectedCommand) ? selectedCommand ?? "none" : "none",
                    ["SelectedCommandResult"] = session.StepResult.TryGetValue("SelectedCommandResult", out var selectedCommandResult) ? selectedCommandResult ?? "none" : "none",
                    ["CorrectedUserMessage"] = session.StepResult.TryGetValue("CorrectedUserMessage", out var correctedUserMessage) ? correctedUserMessage ?? "none" : "none",
                    ["DetectedSwaggerRoutes"] = session.StepResult.TryGetValue("DetectedSwaggerRoutes", out var detectedSwaggerRoutes) ? detectedSwaggerRoutes ?? "none" : "none"
                };

                // Remove any existing SessionContext prompt from chatHistory (system message containing SessionContext)
                chatHistory.RemoveSystemMessagesContaining("*As Context for you*");
                // Get the SessionContext prompt with user selections
                var sessionContextPrompt = await _promptAndSchemaRegistry.GetPrompt("SessionContext", userSelections);
                Console.WriteLine($"SessionContext:\n{sessionContextPrompt}\n\n");
                chatHistory.Add(new ChatMessageContent(AuthorRole.System, sessionContextPrompt));

                session.SetCurrentStep(stateContext.GetCurrentState(), transition);
                var (nextTransition, isConcluded) = await stateContext.HandleState(session, transition, chatHistory);
                transition = nextTransition;
                shouldProceed = isConcluded;
            }
            while (shouldProceed);

            // Send end of stream message
            await _streamReporter.CompleteStreamAsync();
        }
    }
}