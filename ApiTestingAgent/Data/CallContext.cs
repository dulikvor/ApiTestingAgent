using System.Collections.Concurrent;

namespace ApiTestingAgent.Data
{
    public static class CallContext
    {
        static ConcurrentDictionary<string, AsyncLocal<object?>> State = new ConcurrentDictionary<string, AsyncLocal<object?>>();

        public static void SetData(string name, object? data) =>
            State.GetOrAdd(name, _ => new AsyncLocal<object?>()).Value = data;

        public static object? GetData(string name) =>
            State.TryGetValue(name, out AsyncLocal<object?>? data) ? data?.Value : null;
    }
}