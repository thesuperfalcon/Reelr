using System.Security.Claims;
using backend.Features.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace backend.Features.Users
{
    /// <summary>
    /// Shared accounts for showing Reelr to someone, e.g. in a job interview, listed by user id under "Demo:UserIds".
    /// They can do everything a member does, except change the account itself (name, password, picture, sign-ins)
    /// or delete it, so whoever has the password next finds it as it was.
    /// </summary>
    public class DemoAccounts
    {
        public const string NotAllowed = "The demo account cannot change or delete the account.";

        private readonly HashSet<int> _demoIds;

        public DemoAccounts(IConfiguration configuration)
        {
            _demoIds = (configuration.GetSection("Demo:UserIds").Get<int[]>() ?? []).ToHashSet();
        }

        public bool IsDemo(int? userId) => userId is int id && _demoIds.Contains(id);

        public bool IsDemo(ClaimsPrincipal user) => IsDemo(user.FindUserId());
    }

    /// <summary>Refuses the action with 403 when a demo account calls it (see DemoAccounts).</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class NotForDemoAccountsAttribute : Attribute, IAsyncActionFilter
    {
        public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var demoAccounts = context.HttpContext.RequestServices.GetRequiredService<DemoAccounts>();

            if (demoAccounts.IsDemo(context.HttpContext.User))
            {
                var problems = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
                var problem = problems.CreateProblemDetails(context.HttpContext, StatusCodes.Status403Forbidden, title: DemoAccounts.NotAllowed);
                context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status403Forbidden };
                return Task.CompletedTask;
            }

            return next();
        }
    }
}
