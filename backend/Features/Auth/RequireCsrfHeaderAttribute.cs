using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace backend.Features.Auth;

/// <summary>
/// Refuses a request without the X-Reelr-Csrf header with 403. Put on endpoints that act on the refresh
/// cookie, which the browser would also send on a request another site makes it send.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class RequireCsrfHeaderAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.HttpContext.Request.Headers.ContainsKey(RefreshTokens.CsrfHeader))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = $"The {RefreshTokens.CsrfHeader} header is required."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
