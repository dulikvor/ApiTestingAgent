using ApiTestingAgent.Prompts;
using ApiTestingAgent.Agent;
using Microsoft.SemanticKernel.ChatCompletion;
using ApiTestingAgent.Data.Stream;
using StepResultKey = (string, string);

namespace ApiTestingAgent.StateMachine;

public abstract class State<TTransition>
    where TTransition : Enum
{
    protected readonly ILogger<State<TTransition>> _logger;
    protected readonly StreamReporter _streamReporter;
    protected readonly IPromptAndSchemaRegistry _promptAndSchemaRegistry;
    protected readonly IStateFactory _stateFactory;
    protected IChatCompletionAgent _chatCompletionAgent;

    protected State(
        ILogger<State<TTransition>> logger,
        StreamReporter streamReporter,
        IPromptAndSchemaRegistry promptAndSchemaRegistry,
        IStateFactory stateFactory,
        IChatCompletionAgent chatCompletionAgent)
    {
        _logger = logger;
        _streamReporter = streamReporter;
        _promptAndSchemaRegistry = promptAndSchemaRegistry;
        _stateFactory = stateFactory;
        _chatCompletionAgent = chatCompletionAgent;
    }

    public virtual string GetName() => throw new InvalidOperationException();

    public virtual Task<(ApiTestStateTransitions, bool)> HandleState(StateContext<TTransition> context, Session<TTransition> session, TTransition command, ChatHistory chatHistory)
    {
        throw new InvalidOperationException();
    }
}