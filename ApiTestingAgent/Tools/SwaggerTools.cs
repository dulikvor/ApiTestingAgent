using ApiTestingAgent.Tools.Utitlities;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace ApiTestingAgent.Tools
{
    /// <summary>
    /// Represents a tool for REST API operations.
    /// </summary>
    public class SwaggerTools
    {
        private readonly IGitHubRawContentCdnClient _gitHubRawContentCdnClient;
        private readonly IRestClient _restClient;

        public SwaggerTools(IGitHubRawContentCdnClient gitHubRawContentCdnClient, IRestClient restClient)
        {
            _gitHubRawContentCdnClient = gitHubRawContentCdnClient ?? throw new ArgumentNullException(nameof(gitHubRawContentCdnClient));
            _restClient = restClient ?? throw new ArgumentNullException(nameof(restClient));
        }

        /// <summary>
        /// Retrieves the raw Swagger (OpenAPI) definition for a REST API from a GitHub repository and Parses the methods (HTTP verbs and paths) from a Swagger (OpenAPI v2/v3) JSON string.
        /// The Content property of each SwaggerOperation in the result is returned as a base64-encoded string. Consumers should not attempt to unpack or decode it unless needed; it is provided as-is for transport and storage.
        /// </summary>
        /// <param name="owner">The GitHub organization or user name.</param>
        /// <param name="repo">The GitHub repository name.</param>
        /// <param name="branch">The branch name (e.g., 'main').</param>
        /// <param name="path">The path to the Swagger (OpenAPI) file in the repository.</param>
        /// <returns>A list of SwaggerOperation objects, each Content (if exist) enforced as null.</returns>
        [KernelFunction("get_github_rest_swagger_definition")]
        [Description("Retrieves the raw Swagger (OpenAPI) definition for a REST API from a GitHub repository. The Content property of each SwaggerOperation in the result is returned as a base64-encoded string. Consumers should not attempt to unpack or decode it; it is provided as-is.")]
        public async Task<List<SwaggerOperation>> GetGithubRestSwaggerDefinition(
            [Description("The GitHub organization or user name.")] string owner,
            [Description("The GitHub repository name.")] string repo,
            [Description("The branch name (e.g., 'main').")] string branch,
            [Description("The path to the Swagger (OpenAPI) file in the repository.")] string path)
        {
            // Print tool arguments
            Console.WriteLine($"SwaggerTools.GetRestSwaggerDefinition arguments: owner={owner}, repo={repo}, branch={branch}, path={path}");
            var swaggerJson = await _gitHubRawContentCdnClient.GetRawContent(owner, repo, branch, path);
            var result = SwaggerParser.ParseOperations(swaggerJson);
            // Print tool response
            Console.WriteLine($"SwaggerTools.GetRestSwaggerDefinition response: {System.Text.Json.JsonSerializer.Serialize(result)}");
            // Debug: print the operations before storing in context
            Console.WriteLine("Storing SwaggerOperationsKey in context with operations:");
            foreach (var op in result)
            {
                Console.WriteLine($"  Method: {op.HttpMethod}, Url: {op.Url}, Content: {(op.Content != null ? op.Content.ToJsonString() : "null")}");
            }
            ApiTestingAgent.Data.GlobalContext.SetData("SwaggerOperationsKey", System.Text.Json.JsonSerializer.Serialize(result));
            // Return a version of result without content
            return result.Select(r => new SwaggerOperation
            {
                HttpMethod = r.HttpMethod,
                Url = r.Url,
                Content = null,
                ApiVersion = r.ApiVersion
            }).ToList();
        }

        /// <summary>
        /// Retrieves a dictionary of supported API versions and their respective Swagger JSON routes from a service's /swagger endpoint.
        /// </summary>
        /// <param name="domainName">The base domain name of the service (e.g., https://localhost:5001).</param>
        /// <returns>A dictionary mapping API version names to their Swagger JSON routes.</returns>
        [KernelFunction("get_service_rest_api_versions_swagger_definition")]
        [Description("Retrieves a dictionary of supported API versions and their respective Swagger JSON routes from a service's /swagger endpoint. The result is a dictionary mapping API version names to their Swagger JSON routes.")]
        public async Task<Dictionary<string, string>> GetServiceRestApiVersionsSwaggerDefinition(
            [Description("The base domain name of the service (e.g., https://localhost:5001)." )] string domainName)
        {
            if (string.IsNullOrWhiteSpace(domainName))
                throw new ArgumentNullException(nameof(domainName));

            var url = domainName.TrimEnd('/') + "/swagger";
            var response = await _restClient.InvokeRest("GET", url, new Dictionary<string, string>(), string.Empty);
            if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception($"Failed to retrieve swagger index from {url}: {response.HttpStatusCode}");

            // Parse API versions from HTML using SwaggerParser
            var result = SwaggerParser.ParseApiVersionsFromSwaggerHtml(response.Content);
            return result;
        }

        /// <summary>
        /// Retrieves and parses the Swagger (OpenAPI) definition for a specific API version from a service endpoint.
        /// </summary>
        /// <param name="domainName">The base domain name of the service (e.g., https://localhost:5001).</param>
        /// <param name="swaggerRoute">The route to the Swagger JSON (e.g., /swagger/2023-01-01-preview/swagger.json).</param>
        /// <returns>A list of SwaggerOperation objects, each Content (if exist) enforced as null.</returns>
        [KernelFunction("get_service_rest_swagger_definition")]
        [Description("Retrieves and parses the Swagger (OpenAPI) definition for a specific API version from a service endpoint. The Content property of each SwaggerOperation in the result is returned as a base64-encoded string. Consumers should not attempt to unpack or decode it; it is provided as-is.")]
        public async Task<List<SwaggerOperation>> GetServiceRestSwaggerDefinition(
            [Description("The base domain name of the service (e.g., https://localhost:5001)." )] string domainName,
            [Description("The route to the Swagger JSON (e.g., /swagger/2023-01-01-preview/swagger.json)." )] string swaggerRoute)
        {
            if (string.IsNullOrWhiteSpace(domainName))
                throw new ArgumentNullException(nameof(domainName));
            if (string.IsNullOrWhiteSpace(swaggerRoute))
                throw new ArgumentNullException(nameof(swaggerRoute));

            var url = domainName.TrimEnd('/') + "/" + swaggerRoute.TrimStart('/');
            Console.WriteLine($"SwaggerTools.GetServiceRestSwaggerDefinition arguments: url={url}");
            var response = await _restClient.InvokeRest("GET", url, new Dictionary<string, string>(), string.Empty);
            if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception($"Failed to retrieve swagger definition from {url}: {response.HttpStatusCode}");

            var result = SwaggerParser.ParseOperations(response.Content);
            Console.WriteLine($"SwaggerTools.GetServiceRestSwaggerDefinition response: {System.Text.Json.JsonSerializer.Serialize(result)}");
            Console.WriteLine("Storing SwaggerOperationsKey in context with operations:");
            foreach (var op in result)
            {
                Console.WriteLine($"  Method: {op.HttpMethod}, Url: {op.Url}, Content: {(op.Content != null ? op.Content.ToJsonString() : "null")}");
            }
            ApiTestingAgent.Data.GlobalContext.SetData("SwaggerOperationsKey", System.Text.Json.JsonSerializer.Serialize(result));
            // Return a version of result without content
            return result.Select(r => new SwaggerOperation
            {
                HttpMethod = r.HttpMethod,
                Url = r.Url,
                Content = null,
                ApiVersion = r.ApiVersion
            }).ToList();
        }
    }
}