using System.Collections.Concurrent;

namespace ApiTestingAgent.StateMachine;

public static class SessionStore<TSession, TTransition>
    where TSession : Session<TTransition>, new()
    where TTransition : Enum
{
    static private readonly ConcurrentDictionary<string, Session<TTransition>> Sessions = new ConcurrentDictionary<string, Session<TTransition>>();

    public static Session<TTransition> GetSessions(string user) => Sessions.GetOrAdd(user, new TSession());
}
