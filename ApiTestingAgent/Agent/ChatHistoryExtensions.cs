using ApiTestingAgent.Contracts.Copilot;
using ApiTestingAgent.Data;
using ApiTestingAgent.Prompts;
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

        /// <summary>
        /// Adds up to the latest k messages (of any role, default 30) from a CoPilotChatRequestMessage to the ChatHistory.
        /// </summary>
        public static void AddLatestKMessage(this ChatHistory history, CoPilotChatRequestMessage chatRequestMessage, int k = 30)
        {
            if (chatRequestMessage.Messages == null) return;
            var lastK = chatRequestMessage.Messages
                .TakeLast(k);
            foreach (var message in lastK)
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
        /// Summarizes the chat history using the semantic function and "chatsummarize" prompt.
        /// </summary>
        /// <param name="history">The chat history to summarize</param>
        /// <param name="promptAndSchemaRegistry">The prompt registry to access the semantic function</param>
        /// <param name="maxTokens">Maximum tokens for the summary (default: 200)</param>
        /// <param name="temperature">Temperature setting for randomness (default: 0.3)</param>
        /// <returns>A summary of the chat history</returns>
        /// <example>
        /// Usage:
        /// var summary = await chatHistory.SummarizeAsync(promptRegistry);
        /// </example>
        public static async Task<string> SummarizeAsync(this ChatHistory history,
            IPromptAndSchemaRegistry promptAndSchemaRegistry,
            int maxTokens = 200,
            double temperature = 0.3)
        {
            if (history == null || !history.Any())
                return "No chat history to summarize.";

            // Get kernel from GlobalContext
            var kernel = GlobalContext.GetData("Kernel") as Kernel 
                ?? throw new InvalidOperationException("Kernel not found in GlobalContext");

            // Convert chat history to text format
            var historyText = string.Join("\n", history.Select(msg =>
                $"{msg.Role}: {msg.Content}"));

            // Get or create the semantic function for chat summarization
            var summarizeFunction = promptAndSchemaRegistry.GetSemanticFunction("chatsummarize") ??
                                  promptAndSchemaRegistry.CreateSemanticFunction("chatsummarize", maxTokens, temperature);

            // Create arguments for the function
            var arguments = new KernelArguments
            {
                ["history"] = historyText
            };

            // Invoke the function directly and get the result
            var result = await summarizeFunction.InvokeAsync(kernel, arguments);

            return result.GetValue<string>() ?? "Unable to generate summary.";
        }

        /// <summary>
        /// Creates a new ChatHistory with a summary of older messages and the most recent user message.
        /// This helps reduce token count while preserving context and the latest user input.
        /// </summary>
        /// <param name="coPilotChatRequestMessage">The CoPilot chat request message containing the conversation</param>
        /// <param name="promptAndSchemaRegistry">The prompt registry to access the semantic function</param>
        /// <param name="maxTokens">Maximum tokens for the summary (default: 200)</param>
        /// <param name="temperature">Temperature setting for randomness (default: 0.3)</param>
        /// <returns>A new ChatHistory containing the summary and recent user message</returns>
        /// <example>
        /// Usage:
        /// var condensedHistory = await ChatHistoryExtensions.CreateAndSummarizeAsync(coPilotMessage, promptRegistry);
        /// </example>
        public static async Task<ChatHistory> CreateAndSummarizeAsync(CoPilotChatRequestMessage coPilotChatRequestMessage,
            IPromptAndSchemaRegistry promptAndSchemaRegistry,
            int maxTokens = 200,
            double temperature = 0.3)
        {
            var newHistory = new ChatHistory();

            if (coPilotChatRequestMessage?.Messages == null || !coPilotChatRequestMessage.Messages.Any())
                return newHistory;

            // If there's only one message, just return a copy
            if (coPilotChatRequestMessage.Messages.Count <= 1)
            {
                foreach (var msg in coPilotChatRequestMessage.Messages)
                {
                    var authorRole = msg.Role switch
                    {
                        OpenAI.Chat.ChatMessageRole.User => AuthorRole.User,
                        OpenAI.Chat.ChatMessageRole.Assistant => AuthorRole.Assistant,
                        OpenAI.Chat.ChatMessageRole.System => AuthorRole.System,
                        _ => AuthorRole.User
                    };
                    newHistory.Add(new ChatMessageContent(authorRole, msg.Content ?? string.Empty));
                }
                return newHistory;
            }

            // Find the most recent user message
            CopilotChatMessage? mostRecentUserMessage = null;
            for (int i = coPilotChatRequestMessage.Messages.Count - 1; i >= 0; i--)
            {
                if (coPilotChatRequestMessage.Messages[i].Role == OpenAI.Chat.ChatMessageRole.User)
                {
                    mostRecentUserMessage = coPilotChatRequestMessage.Messages[i];
                    break;
                }
            }

            // If no user message found, just summarize everything
            if (mostRecentUserMessage == null)
            {
                // Convert all messages to ChatHistory for summarization
                var tempHistory = new ChatHistory();
                tempHistory.AddCoPilotChatRequestMessages(coPilotChatRequestMessage);
                var fullSummary = await tempHistory.SummarizeAsync(promptAndSchemaRegistry, maxTokens, temperature);
                newHistory.Add(new ChatMessageContent(AuthorRole.System, $"Previous conversation summary: {fullSummary}"));
                return newHistory;
            }

            // Create a history without the most recent user message for summarization
            var messagesToSummarize = new List<CopilotChatMessage>();
            foreach (var msg in coPilotChatRequestMessage.Messages)
            {
                if (msg != mostRecentUserMessage)
                {
                    messagesToSummarize.Add(msg);
                }
                else
                {
                    break; // Stop when we reach the most recent user message
                }
            }

            // Generate summary if there are messages to summarize
            if (messagesToSummarize.Any())
            {
                var tempChatRequest = new CoPilotChatRequestMessage { Messages = messagesToSummarize };
                var historyToSummarize = new ChatHistory();
                historyToSummarize.AddCoPilotChatRequestMessages(tempChatRequest);
                var summary = await historyToSummarize.SummarizeAsync(promptAndSchemaRegistry, maxTokens, temperature);
                newHistory.Add(new ChatMessageContent(AuthorRole.System, $"Previous conversation summary: {summary}"));
            }

            // Add the most recent user message
            var recentUserRole = mostRecentUserMessage.Role switch
            {
                OpenAI.Chat.ChatMessageRole.User => AuthorRole.User,
                OpenAI.Chat.ChatMessageRole.Assistant => AuthorRole.Assistant,
                OpenAI.Chat.ChatMessageRole.System => AuthorRole.System,
                _ => AuthorRole.User
            };
            newHistory.Add(new ChatMessageContent(recentUserRole, mostRecentUserMessage.Content ?? string.Empty));

            return newHistory;
        }
    }
}
