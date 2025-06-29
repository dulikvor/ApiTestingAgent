using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ApiTestingAgent.Agent
{
    public interface IChatCompletionAgent
    {
        // Define methods for chat completion agent
        Task<IEnumerable<ChatMessageContent>> GetChatCompletionAsync(ChatHistory chatHistory, CancellationToken cancellationToken = default);
        Task<ChatMessageContent> PlanInvokeAsync(ChatHistory chatHistory, CancellationToken cancellationToken = default);
    }
}
