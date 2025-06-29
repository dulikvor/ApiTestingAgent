namespace ApiTestingAgent.StateMachine
{
    public interface IStateFactory
    {
        State<TTransition> Create<TState, TTransition>()
            where TState : State<TTransition>
            where TTransition : System.Enum;

        State<TTransition> Create<TTransition>(Type stateType)
            where TTransition : System.Enum;
    }
}
