using ApiTestingAgent.Contracts.Copilot;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ApiTestingAgent.Agent
{
    public static class ChatHistoryExtensions
    {
        /// <summary>
        /// Adds all messages from a CoPilotChatRequestMessage to the ChatHistory.
        /// </summary>
        public static void AddCoPilotChatRequestMessages(this ChatHistory history, CoPilotChatRequestMessage chatRequestMessage)
        {
            if (chatRequestMessage.Messages == null) return;
            foreach (var message in chatRequestMessage.Messages)
            {
                var authorRole = message.Role switch
                {
                    OpenAI.Chat.ChatMessageRole.User => AuthorRole.User,
                    OpenAI.Chat.ChatMessageRole.Assistant => AuthorRole.Assistant,
                    OpenAI.Chat.ChatMessageRole.System => AuthorRole.System,
                    _ => AuthorRole.User
                };
                history.Add(new ChatMessageContent(authorRole, message.Content ?? string.Empty));
            }
        }

        /// <summary>
        /// Removes all system messages from the ChatHistory whose content contains the given substring.
        /// </summary>
        public static void RemoveSystemMessagesContaining(this ChatHistory history, string substring)
        {
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var msg = history[i];
                if (msg.Role == AuthorRole.System && msg.Content?.Contains(substring) == true)
                {
                    history.RemoveAt(i);
                }
            }
        }
    }
}
