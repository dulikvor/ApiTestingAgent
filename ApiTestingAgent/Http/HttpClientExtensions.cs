using ApiTestingAgent.Http;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using System.Text.Json;

namespace ApiTestingAgent.Http
{
    public static class HttpClientExtensions
    {
        public static async Task<TResponse> GetAsync<TResponse>(
            this HttpClient httpClient,
            string uri,
            Dictionary<string, string>? headers = null,
            CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(httpClient.BaseAddress ?? new Uri(""), uri));

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var detailedErrorMessage = response.Content != null
                    ? await response.Content.ReadAsStringAsync(cancellationToken)
                    : string.Empty;

                throw new HttpResponseException(
                    response.StatusCode,
                    $"HttpClient: Response status code does not indicate success HttpStatusCode: {(int)response.StatusCode}, DetailedError: {detailedErrorMessage}.");
            }

            if (response.Content != null)
            {
                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (contentType == MediaTypeNames.Application.Json)
                {
                    var result = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken);
                    if (result == null)
                    {
                        throw new InvalidOperationException("The response content could not be deserialized to the specified type.");
                    }
                    return result;
                }
                else if (contentType == MediaTypeNames.Text.Plain && typeof(TResponse) == typeof(string))
                {
                    return (TResponse)(object)await response.Content.ReadAsStringAsync(cancellationToken);
                }
            }

            return default!;
        }

        public static async Task<TResponse> PostAsync<TResponse, TContent>(
            this HttpClient httpClient,
            string uri,
            TContent content,
            Dictionary<string, string>? headers = null,
            CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(httpClient.BaseAddress ?? new Uri(""), uri));

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }
            request.Content = GetHttpStringContent(content!);

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var detailedErrorMessage = response.Content != null
                    ? await response.Content.ReadAsStringAsync(cancellationToken)
                    : string.Empty;

                throw new HttpResponseException(
                    response.StatusCode,
                    $"HttpClient: Response status code does not indicate success - {(int)response.StatusCode} ({response.ReasonPhrase}){detailedErrorMessage}.");
            }

            if (response.Content != null && response.Content.Headers.ContentType?.MediaType == MediaTypeNames.Application.Json)
            {
                var result = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken);
                if (result == null)
                {
                    throw new InvalidOperationException("The response content could not be deserialized to the specified type.");
                }
                return result;
            }

            return default!;
        }

        private static StringContent? GetHttpStringContent(object body)
        {
            if (body == null)
            {
                return null;
            }

            return new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                MediaTypeNames.Application.Json);
        }
    }
}
