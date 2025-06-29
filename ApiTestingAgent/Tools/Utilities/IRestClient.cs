using System.Net;
using ApiTestingAgent.Tools;

namespace ApiTestingAgent.Tools.Utitlities
{
    public interface IRestClient
    {
        Task<RestResponse> InvokeRest(string method, string url, Dictionary<string, string> headers, string body);
    }
}