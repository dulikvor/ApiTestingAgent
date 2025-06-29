using ApiTestingAgent.Tools.Utitlities;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace ApiTestingAgent.Tools
{
    /// <summary>
    /// Represents a tool for REST API operations.
    /// </summary>
    public class SwaggerTools
    {
        private readonly IGitHubRawContentCdnClient _gitHubRawContentCdnClient;

        public SwaggerTools(IGitHubRawContentCdnClient gitHubRawContentCdnClient)
        {
            _gitHubRawContentCdnClient = gitHubRawContentCdnClient ?? throw new ArgumentNullException(nameof(gitHubRawContentCdnClient));
        }

        /// <summary>
        /// Retrieves the raw Swagger (OpenAPI) definition for a REST API from a GitHub repository.
        /// </summary>
        /// <param name="owner">The GitHub organization or user name.</param>
        /// <param name="repo">The GitHub repository name.</param>
        /// <param name="branch">The branch name (e.g., 'main').</param>
        /// <param name="path">The path to the Swagger (OpenAPI) file in the repository.</param>
        /// <returns>The raw Swagger definition as a string.</returns>
        [KernelFunction("get_rest_swagger_definition")]
        [Description("Retrieves the raw Swagger (OpenAPI) definition for a REST API from a GitHub repository.")]
        public async Task<string> GetRestSwaggerDefinition(
            [Description("The GitHub organization or user name.")] string owner,
            [Description("The GitHub repository name.")] string repo,
            [Description("The branch name (e.g., 'main').")] string branch,
            [Description("The path to the Swagger (OpenAPI) file in the repository.")] string path)
        {
            return await _gitHubRawContentCdnClient.GetRawContent(owner, repo, branch, path);
        }

        /// <summary>
        /// Parses the methods (HTTP verbs and paths) from a Swagger (OpenAPI v2/v3) JSON string.
        /// </summary>
        /// <param name="swaggerJson">The Swagger (OpenAPI) JSON string.</param>
        /// <returns>A dictionary where the key is the HTTP method (GET, POST, etc.) and the value is a list of paths for that method.</returns>
        [KernelFunction("parse_swagger_methods")]
        [Description("Parses the HTTP methods and paths from a Swagger (OpenAPI) JSON string.")]
        public List<SwaggerOperation> ParseSwaggerMethods(
            [Description("The Swagger (OpenAPI) JSON string.")] string swaggerJson)
        {
            return SwaggerParser.ParseOperations(swaggerJson);
        }
    }
}
