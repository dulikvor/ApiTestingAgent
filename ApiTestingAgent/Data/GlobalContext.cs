using System.Collections.Concurrent;

namespace ApiTestingAgent.Data
{
    public static class GlobalContext
    {
        private static readonly ConcurrentDictionary<string, object?> State = new ConcurrentDictionary<string, object?>();

        public static void SetData(string name, object? data) =>
            State[name] = data;

        public static object? GetData(string name) =>
            State.TryGetValue(name, out var data) ? data : null;
    }
}
