using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiTestingAgent.Prompts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ApiTestingAgent.Data;
using System.Runtime.CompilerServices;

namespace ApiTestingAgent.Controllers
{
    [ApiController]
    [Route("api/prompts")]
    public class PromptController : ControllerBase
    {
        private readonly IPromptAndSchemaRegistry _promptRegistry;
        private readonly IOptions<FeaturesConfiguration> _featuresConfig;

        public PromptController(IPromptAndSchemaRegistry promptRegistry, IOptions<FeaturesConfiguration> featuresConfig)
        {
            _promptRegistry = promptRegistry;
            _featuresConfig = featuresConfig;
        }

        [HttpPost("override")]
        public async Task<IActionResult> OverridePrompt([FromQuery] string key)
        {
            using var reader = new StreamReader(Request.Body);
            var newPrompt = await reader.ReadToEndAsync();
            // Only allow in development or if AllowPromptOverride is true
            var allowOverride = _featuresConfig.Value.AllowPromptOverride;
            if (!allowOverride)
                return Forbid("Prompt override is not allowed in this environment.");

            try
            {
                _promptRegistry.OverridePrompt(key, newPrompt);
                return Ok($"Prompt '{key}' overridden successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{key}")]
        public IActionResult GetPrompt([FromRoute] string key)
        {
            // Only allow in development or if AllowPromptOverride is true
            var allowOverride = _featuresConfig.Value.AllowPromptOverride;
            if (!allowOverride)
                return Forbid("Prompt retrieval is not allowed in this environment.");

            try
            {
                var prompt = _promptRegistry.GetPrompt(key).GetAwaiter().GetResult();
                return Ok(prompt);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
