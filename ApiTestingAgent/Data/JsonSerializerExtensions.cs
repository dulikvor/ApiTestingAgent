using System.Text.Json;

namespace ApiTestingAgent.Data
{
    /// <summary>
    /// Extension methods for JsonSerializer to handle cleaning of response content
    /// </summary>
    public static class JsonSerializerExtensions
    {
        /// <summary>
        /// List of tokens that may appear at the start of a response that should be removed
        /// </summary>
        private static readonly string[] StartTokens = new[]
        {
            "```json",
            "```JSON",
            "```",
            "json:",
            "JSON:",
            "Response:",
            "Output:",
            "Result:"
        };

        /// <summary>
        /// List of tokens that may appear at the end of a response that should be removed
        /// </summary>
        private static readonly string[] EndTokens = new[]
        {
            "```",
            "```json",
            "```JSON"
        };

        /// <summary>
        /// Deserializes JSON content after cleaning it of markdown tokens and common prefixes/suffixes
        /// </summary>
        /// <typeparam name="T">The type to deserialize to</typeparam>
        /// <param name="content">The raw JSON content that may contain markdown tokens</param>
        /// <param name="options">Optional JsonSerializer options</param>
        /// <returns>Deserialized object of type T</returns>
        public static T? DeserializeClean<T>(string content, JsonSerializerOptions? options = null)
        {
            var cleanedContent = CleanJsonResponse(content);
            return JsonSerializer.Deserialize<T>(cleanedContent, options);
        }

        /// <summary>
        /// Cleans JSON response content by removing markdown code block tokens and other common prefixes/suffixes.
        /// Only one start token and one end token will match and be removed.
        /// </summary>
        /// <param name="content">The raw response content</param>
        /// <returns>Cleaned JSON string ready for deserialization</returns>
        private static string CleanJsonResponse(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return content;

            var cleaned = content.Trim();

            // Remove start tokens - only the first match will be removed
            foreach (var startToken in StartTokens)
            {
                if (cleaned.StartsWith(startToken, StringComparison.OrdinalIgnoreCase))
                {
                    cleaned = cleaned.Substring(startToken.Length).Trim();
                    break; // Only remove one start token
                }
            }

            // Remove end tokens - only the first match will be removed
            foreach (var endToken in EndTokens)
            {
                if (cleaned.EndsWith(endToken, StringComparison.OrdinalIgnoreCase))
                {
                    cleaned = cleaned.Substring(0, cleaned.Length - endToken.Length).Trim();
                    break; // Only remove one end token
                }
            }

            // Remove any remaining leading/trailing whitespace or newlines
            cleaned = cleaned.Trim('\r', '\n', ' ', '\t');

            return cleaned;
        }
    }
}
