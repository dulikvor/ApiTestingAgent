using System.ComponentModel.DataAnnotations;

namespace ApiTestingAgent.Data
{
    public class ServiceHttpClientOptions
    {
        public Uri? Endpoint { get; set; }
        public string? Audience { get; set; }
        public string? ApiKey { get; set; }
    }
}
