using ApiTestingAgent.Data;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApiTestingAgent.Authentication
{
    public class GitHubAuthenticationContextFilter : ActionFilterAttribute
    {
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            CallContext.SetData("UserNameKey", context.HttpContext.User.Identities.First(i => !string.IsNullOrEmpty(i.Name)).Name);
            await next();
        }
    }
}
