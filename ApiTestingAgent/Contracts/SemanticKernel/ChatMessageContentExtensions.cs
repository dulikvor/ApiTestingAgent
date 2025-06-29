using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ApiTestingAgent.Contracts.SemanticKernel
{
    public static class ChatMessageContentExtensions
    {
        public static ChatMessageContent CloneWithContent(this ChatMessageContent original, string newContent)
        {
            return new ChatMessageContent(
                original.Role,
                newContent,
                original.ModelId,
                original.InnerContent,
                original.Encoding,
                original.Metadata
            );
        }
    }
}
