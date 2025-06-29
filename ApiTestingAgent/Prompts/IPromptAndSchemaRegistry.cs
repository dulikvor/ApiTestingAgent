namespace ApiTestingAgent.Prompts
{
    public interface IPromptAndSchemaRegistry
    {
        Task<string> GetPrompt(string key, Dictionary<string, string>? extraArgs = null);
        string? GetSchema(string key);
        Type? GetSchemaUserType(string key);
        IEnumerable<string> RegisteredPromptKeys { get; }
        IEnumerable<string> RegisteredSchemaKeys { get; }
    }
}
