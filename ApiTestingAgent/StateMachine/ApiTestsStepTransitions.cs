namespace ApiTestingAgent.StateMachine
{
    public enum ApiTestStateTransitions
    {
        DomainSelect,
        RestDiscovery,
        RawContentGet,
        RestCompile,
        CommandSelect,
        ExecutionPlanSelect,
        ExpectedOutcome,
        CommandInvocation,
        CommandInvocationAnalysis,
        Any
    }
}
