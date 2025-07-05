using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace ApiTestingAgent.Authentication
{
    public class LocalChatAuthenticationSchemeOptions : AuthenticationSchemeOptions
    {
        public string? AllowedAppName { get; set; } = "LocalChatApp";
    }

    public class LocalChatAuthenticationHandler : AuthenticationHandler<LocalChatAuthenticationSchemeOptions>
    {
        public const string LocalChatScheme = "LocalChat";

        public LocalChatAuthenticationHandler(
            IOptionsMonitor<LocalChatAuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // For local chat, we bypass authentication but optionally validate app name
            var appNameHeader = Request.Headers["X-App-Name"].FirstOrDefault();
            
            if (!string.IsNullOrEmpty(Options.AllowedAppName) && 
                !string.IsNullOrEmpty(appNameHeader) && 
                !appNameHeader.Equals(Options.AllowedAppName, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid app name"));
            }

            // Create a simple claims identity for local chat
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "LocalChatUser"),
                new Claim(ClaimTypes.AuthenticationMethod, LocalChatScheme),
                new Claim("app_name", appNameHeader ?? "unknown")
            };

            var identity = new ClaimsIdentity(claims, LocalChatScheme);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, LocalChatScheme);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = 401;
            return Task.CompletedTask;
        }

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = 403;
            return Task.CompletedTask;
        }
    }
}
