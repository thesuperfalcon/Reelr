using System.Security.Claims;

namespace backend.Features.Auth
{
    public static class ClaimsPrincipalExtensions
    {
        // The signed-in user's id. Only for endpoints behind [Authorize], where the claim is always present.
        public static int GetUserId(this ClaimsPrincipal user) =>
            int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // The caller's id, or null for an anonymous caller.
        public static int? FindUserId(this ClaimsPrincipal user) =>
            int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    }
}
