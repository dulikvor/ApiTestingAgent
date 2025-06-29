using Microsoft.SemanticKernel.ChatCompletion;

namespace ApiTestingAgent.StateMachine;

public class StateContext<TTransition> 
    where TTransition : Enum
{
    private State<TTransition> _currentState;

    public StateContext(State<TTransition> startingState)
    {
        _currentState = startingState;
    }

    public async Task<(ApiTestStateTransitions, bool)> HandleState(Session<TTransition> session, TTransition commandType, ChatHistory chatHistory)
    {
        return await _currentState.HandleState(this, session, commandType, chatHistory);
    }

    public void SetState(State<TTransition> nextState)
    {
        _currentState = nextState;
    }

    public State<TTransition> GetCurrentState()
    {
        return _currentState;
    }
}

