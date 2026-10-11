using System.Security.Claims;
using backend.Features.Auth;

namespace backend.Features.Moderation
{
    // Admins are the user ids listed in "Admin:UserIds". Ids, not usernames: a name can be changed,
    // and someone else could then take an admin's old name. The list is read at start-up and checked on
    // every request, so removing an id and restarting takes the rights away; no token has to expire first.
    public class AdminAccess
    {
        public const string Policy = "Admin";

        private readonly HashSet<int> _adminIds;

        public AdminAccess(IConfiguration configuration)
        {
            _adminIds = (configuration.GetSection("Admin:UserIds").Get<int[]>() ?? []).ToHashSet();
        }

        public bool IsAdmin(int? userId) => userId is int id && _adminIds.Contains(id);

        public bool IsAdmin(ClaimsPrincipal user) => IsAdmin(user.FindUserId());
    }
}
