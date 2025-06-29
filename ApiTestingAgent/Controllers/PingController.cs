using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiTestingAgent.Controllers
{
    [AllowAnonymous]
    [ApiController]
    public class PingController : ControllerBase
    {
        private const string PingResponse = "Api Testing Agent saying hello :)";

        [HttpGet]
        [Route("")]
        [Route("/favicon.ico")]
        public string GetHealthStatus()
        {
            return PingResponse;
        }
    }
}