namespace ApiTestingAgent.Data.Stream;

public interface IResponseStreamWriter<T> where T : IResponseStreamWriter<T>
{
    void StartStream(HttpContext httpContext);
    Task WriteToStreamAsync(HttpContext httpContext, IReadOnlyList<object> messages, object? eventType = null);
    Task CompleteStream(HttpContext httpContext);
}