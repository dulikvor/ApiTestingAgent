using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SemanticChatCompletionAgent = Microsoft.SemanticKernel.Agents.ChatCompletionAgent;

namespace ApiTestingAgent.Agent
{
    public class ChatCompletionAgent : IChatCompletionAgent
    {
        private SemanticChatCompletionAgent? _chatCompletionAgent;
        private IChatCompletionService? _chatCompletionService;
        private Kernel? _kernel;
        private bool _initialized = false;

        public async Task<IEnumerable<ChatMessageContent>> GetChatCompletionAsync(ChatHistory chatHistory, CancellationToken cancellationToken = default)
        {
            if (_chatCompletionAgent == null)
                throw new InvalidOperationException("ChatCompletionAgent is not initialized.");
            
            var chatMessages = new List<ChatMessageContent>();
            await foreach (var response in _chatCompletionAgent.InvokeAsync(chatHistory, cancellationToken: cancellationToken))
            {
                chatMessages.Add(response.Message);
            }
            return chatMessages;
        }

        public async Task<ChatMessageContent> PlanInvokeAsync(ChatHistory chatHistory, CancellationToken cancellationToken = default)
        {
            OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new() 
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            return await _chatCompletionService!.GetChatMessageContentAsync(chatHistory, executionSettings: openAIPromptExecutionSettings, kernel: _kernel!);
        }

        public void Initialize(Kernel kernel)
        {
            if (_initialized) return;
            _chatCompletionAgent = new SemanticChatCompletionAgent()
            {
                Kernel = kernel
            };
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel), "Kernel cannot be null.");
            _chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
            _initialized = true;
        }
    }
}
