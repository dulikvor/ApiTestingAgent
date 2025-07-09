using ApiTestingAgent.Contracts.Copilot;
using Microsoft.SemanticKernel;
using System.Text.Json;

namespace ApiTestingAgent.Data.Stream;

public class CopilotServerSentEventsStreamWriter : IResponseStreamWriter<CopilotServerSentEventsStreamWriter>, IStreamWriter
{
    public CopilotServerSentEventsStreamWriter()
    {
    }

    public void StartStream(HttpContext httpContext)
    {
        httpContext.Response.ContentType = "text/event-stream";
    }

    public async Task WriteToStreamAsync(HttpContext httpContext, IReadOnlyList<object> messages, object? eventType = null)
    {
        CopilotEventType copilotEventType = CopilotEventType.Message;
        if (eventType is CopilotEventType cet)
        {
            copilotEventType = cet;
        }
        else if (eventType is string s && Enum.TryParse<CopilotEventType>(s, true, out var parsed))
        {
            copilotEventType = parsed;
        }
        // else: fallback to default

        foreach (var message in messages)
        {
            if (message is ChatMessageContent chatMessage)
            {
                // Use the new constructor to transform to CoPilotChatResponseMessage
                var response = new CoPilotChatResponseMessage(chatMessage);
                var serializedMessage = JsonSerializer.Serialize(response);
                var messageString = copilotEventType == CopilotEventType.Message
                    ? "data: " + serializedMessage + "\n\n"
                    : "event: " + copilotEventType.ToSerializedString() + "\ndata: " + serializedMessage + "\n\n";
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

    public async Task CompleteStream(HttpContext httpContext)
    {
        var completionMessage = "data: [DONE]\n\n";
        await httpContext.Response.WriteAsync(completionMessage);
        await httpContext.Response.Body.FlushAsync();
    }

    // New overload for sending a plain text event
    public Task WriteTextToStreamAsync(HttpContext httpContext, string message)
    {
        throw new NotImplementedException();
    }
}