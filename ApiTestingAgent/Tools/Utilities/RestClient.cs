using System.Net;
using System.Text;
using System.Net.Http;
using ApiTestingAgent.Data;
using ApiTestingAgent.Tools;

namespace ApiTestingAgent.Tools.Utitlities
{
    public class RestClient : IRestClient
    {
        private readonly HttpClient _httpClient;

        public RestClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<RestResponse> InvokeRest(string method, string url, Dictionary<string, string> headers, string body)
        {
            ArgumentValidationHelper.Ensure.NotNull(method, nameof(method));
            ArgumentValidationHelper.Ensure.NotNull(url, nameof(url));

            var request = new HttpRequestMessage(new HttpMethod(method), url);

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    // Content headers must be set on request.Content, not request.Headers
                    if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                    {
                        // Will be set below when creating StringContent
                        continue;
                    }
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            if (!string.IsNullOrEmpty(body))
            {
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                // If user provided a Content-Type header, override the default
                if (headers != null && headers.TryGetValue("Content-Type", out var contentType))
                {
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                }
                request.Content = content;
            }

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            return new RestResponse
            {
                HttpStatusCode = response.StatusCode,
                Content = responseContent
            };
        }
    }
}