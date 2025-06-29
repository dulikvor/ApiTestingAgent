namespace ApiTestingAgent.StateMachine
{
    /// <summary>
    /// Factory for creating transient State instances from the DI container.
    /// </summary>
    public class StateFactory : IStateFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public StateFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Creates a transient State instance of the specified type from DI, ensuring it derives from State<TTransition, TStepInput>.
        /// </summary>
        /// <typeparam name="TState">The type of State to create.</typeparam>
        /// <typeparam name="TTransition">The transition enum type.</typeparam>
        /// <typeparam name="TStepInput">The step input type.</typeparam>
        /// <returns>A new instance of the requested State type.</returns>
        public State<TTransition> Create<TState, TTransition>()
            where TState : State<TTransition>
            where TTransition : Enum
        {
            return (State<TTransition>)_serviceProvider.GetRequiredService(typeof(TState));
        }

        /// <summary>
        /// Creates a transient State instance by type (useful for dynamic scenarios), ensuring it derives from State<TTransition, TStepInput>.
        /// </summary>
        /// <typeparam name="TTransition">The transition enum type.</typeparam>
        /// <typeparam name="TStepInput">The step input type.</typeparam>
        /// <param name="stateType">The type of State to create.</param>
        /// <returns>A new instance of the requested State type.</returns>
        public State<TTransition> Create<TTransition>(Type stateType)
            where TTransition : Enum
        {
            return (State<TTransition>)_serviceProvider.GetRequiredService(stateType);
        }
    }
}
