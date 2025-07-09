using Microsoft.SemanticKernel;
using System.Text.Json;

namespace ApiTestingAgent.Data.Stream;

public class LocalChatServerSentEventsStreamWriter : IResponseStreamWriter<LocalChatServerSentEventsStreamWriter>, IStreamWriter
{
    public LocalChatServerSentEventsStreamWriter()
    {
    }

    public void StartStream(HttpContext httpContext)
    {
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers["Cache-Control"] = "no-cache";
        httpContext.Response.Headers["Connection"] = "keep-alive";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";
    }

    public async Task WriteToStreamAsync(HttpContext httpContext, IReadOnlyList<object> messages, object? eventType = null)
    {
        foreach (var message in messages)
        {
            if (message is ChatMessageContent chatMessage)
            {
                // For local chat, we'll send the expected message format
                var localMessage = new
                {
                    message = chatMessage.Content,
                    role = chatMessage.Role.ToString().ToLower()
                };
                var serializedMessage = JsonSerializer.Serialize(localMessage);
                var messageString = serializedMessage + "\n\n";
                await httpContext.Response.WriteAsync(messageString);
                await httpContext.Response.Body.FlushAsync();
            }
            else
            {
                // fallback for non-chat messages
                var serializedMessage = JsonSerializer.Serialize(message);
                var messageString = "data: " + serializedMessage + "\n\n";
                await httpContext.Response.WriteAsync(messageString);
                await httpContext.Response.Body.FlushAsync();
            }
        }
    }

    public async Task WriteTextToStreamAsync(HttpContext httpContext, string message)
    {
        var eventString = "event: text\ndata: " + message + "\n\n";
        await httpContext.Response.WriteAsync(eventString);
        await httpContext.Response.Body.FlushAsync();
    }

    public async Task CompleteStream(HttpContext httpContext)
    {
        var completionMessage = "data: [DONE]";
        await httpContext.Response.WriteAsync(completionMessage);
        await httpContext.Response.Body.FlushAsync();
    }
}
