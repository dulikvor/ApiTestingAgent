namespace ApiTestingAgent.StateMachine
{
    public enum ApiTestStateTransitions
    {
        DomainSelect,
        RestDiscovery,
        RawContentGet,
        RestCompile,
        CommandSelect,
        ExpectedOutcome,
        CommandInvocation,
        CommandInvocationAnalysis,
        Any
    }
}
