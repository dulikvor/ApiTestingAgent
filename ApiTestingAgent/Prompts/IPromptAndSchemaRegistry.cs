using Microsoft.SemanticKernel;

namespace ApiTestingAgent.Prompts
{
    public interface IPromptAndSchemaRegistry
    {
        Task<string> GetPrompt(string key, Dictionary<string, string>? extraArgs = null);
        string? GetSchema(string key);
        Type? GetSchemaUserType(string key);
        IEnumerable<string> RegisteredPromptKeys { get; }
        IEnumerable<string> RegisteredSchemaKeys { get; }
        // Allow runtime override of prompts (for development/testing only)
        void OverridePrompt(string key, string newPrompt);
        
        // Semantic function creation and management methods
        KernelFunction CreateSemanticFunction(string key, int maxTokens = 500, double temperature = 0.5);
        KernelFunction? GetSemanticFunction(string key);
    }
}
