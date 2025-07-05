namespace ApiTestingAgent.Data
{
    public class ChatConfiguration
    {
        public string ChatType { get; set; } = "CopilotChat";
        public string? AllowedAppName { get; set; } = "LocalChatApp"; // For local chat authentication
        
        public bool IsCopilotChat => ChatType.Equals("CopilotChat", StringComparison.OrdinalIgnoreCase);
        public bool IsLocalChat => ChatType.Equals("LocalChat", StringComparison.OrdinalIgnoreCase);
    }

    public enum ChatType
    {
        CopilotChat,
        LocalChat
    }
}
