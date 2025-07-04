using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Net;
using ApiTestingAgent.Tools.Utitlities;
using System.Text.Json.Serialization;

namespace ApiTestingAgent.Tools
{
    public class RestResponse
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("httpStatusCode")]
        public HttpStatusCode HttpStatusCode { get; set; }
    }
    /// <summary>
    /// Tool for invoking REST API operations.
    /// </summary>
    public class RestTools
    {
        private readonly IRestClient _restClient;

        public RestTools(IRestClient restClient)
        {
            _restClient = restClient ?? throw new ArgumentNullException(nameof(restClient));
        }

        /// <summary>
        /// Invokes a REST API using the provided method, url, headers, and body.
        /// </summary>
        /// <param name="method">The HTTP method to use (e.g., 'GET', 'POST').</param>
        /// <param name="url">The URL of the REST API endpoint.</param>
        /// <param name="headers">The headers to include in the request.</param>
        /// <param name="body">The body of the request, if applicable.</param>
        /// <returns>Tuple of HttpStatusCode and response content</returns>
        [KernelFunction("rest_invoke")]
        [Description("Invokes a REST API endpoint with the specified HTTP method, URL, and optional body. " +
        "Returns a RestResponse object with two fields: " +
        "HttpStatusCode httpStatusCode, string content. " +
        "The 'httpStatusCode' represents the numeric HTTP status code returned (e.g., 200, 400, 500). " +
        "The 'content' is the raw string body of the HTTP response, which may be JSON or plain text.")]
        public async Task<RestResponse> InvokeRestAsync(
            [Description("The HTTP method to use (e.g., 'GET', 'POST').")] string method,
            [Description("The URL of the REST API endpoint.")] string url,
            [Description("The body of the request, if applicable.")] string body)
        {
            // Print tool arguments (excluding body/content)
            Console.WriteLine($"RestTools.InvokeRestAsync arguments: method={method}, url={url}");
            var result = await _restClient.InvokeRest(method, url, null!, body);
            // Print only the HTTP status code in the response
            Console.WriteLine($"RestTools.InvokeRestAsync response HttpStatusCode: {result.HttpStatusCode}");
            return new RestResponse
            {
                HttpStatusCode = result.HttpStatusCode,
                Content = result.Content
            };
        }
    }
}
