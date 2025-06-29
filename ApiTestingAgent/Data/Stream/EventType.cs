using System;
using System.Text.Json.Serialization;

namespace ApiTestingAgent.Data.Stream
{
    public enum CopilotEventType
    {
        Message,
        CopilotConfirmation
    }

    public static class CopilotEventTypeExtensions
    {
        public static string ToSerializedString(this CopilotEventType eventType)
        {
            return eventType switch
            {
                CopilotEventType.Message => "message",
                CopilotEventType.CopilotConfirmation => "copilot_confirmation",
                _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null)
            };
        }
    }
}