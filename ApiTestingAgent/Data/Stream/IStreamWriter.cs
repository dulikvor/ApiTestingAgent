namespace ApiTestingAgent.Data.Stream
{
    public interface IStreamWriter
    {
        void StartStream(HttpContext httpContext);
        Task WriteToStreamAsync(HttpContext httpContext, IReadOnlyList<object> messages, object? eventType = null);
        Task CompleteStream(HttpContext httpContext);
    }
}
