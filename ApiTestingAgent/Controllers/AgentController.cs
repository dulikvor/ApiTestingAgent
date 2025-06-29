using ApiTestingAgent.Agent;
using ApiTestingAgent.Contracts.Copilot;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiTestingAgent.StateMachine;
using ApiTestingAgent.Prompts;
using ApiTestingAgent.Services;
using ApiTestingAgent.Data.Stream;
using ApiTestingAgent.Authentication;

namespace ApiTestingAgent.Controllers;

[ApiController]
[Authorize]
[GitHubAuthenticationContextFilter]
public class AgentController : ControllerBase
{
    private readonly IApiTestService _apiTestService;

    public AgentController(
        IApiTestService apiTestService)
    {
        _apiTestService = apiTestService;
    }

    [HttpPost("/nextEvent")]
    public async Task NextEvent([FromBody] CoPilotChatRequestMessage chatRequestMessage)
    {
        await _apiTestService.InvokeNext(HttpContext, chatRequestMessage);
    }
}
