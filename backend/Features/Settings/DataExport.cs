using backend.Data;
using backend.Features.Users;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.Settings
{
    // Everything Reelr stores about one user, in a form they can read and take elsewhere (GDPR article 15 and 20).
    // Other people's content is left out, except the usernames of who the user follows, who follows them and who
    // they blocked. Reports keep only what the user filed, not what the admins noted or who resolved them.
    public static class DataExport
    {
        public static async Task<object> BuildAsync(ReelrContext context, User user)
        {
            var id = user.Id;

            var avatar = await context.UserAvatars
                .Where(a => a.UserId == id)
                .Select(a => new { a.ContentType, a.Data, a.UpdatedAt })
                .FirstOrDefaultAsync();

            return new
            {
                ExportedAt = DateTime.UtcNow,
                Account = new
                {
                    user.UserName,
                    user.Email,
                    user.EmailConfirmed,
                    user.TermsAcceptedAt,
                    user.WatchlistVisibility,
                    user.DiaryVisibility,
                    user.ShowFriendReviews,
                    user.ShowOwnActivity,
                    ProfilePicture = avatar == null
                        ? null
                        : new { avatar.ContentType, avatar.UpdatedAt, Base64 = Convert.ToBase64String(avatar.Data) }
                },
                Diary = await context.DiaryEntries
                    .Where(d => d.UserId == id)
                    .OrderBy(d => d.WatchedAt)
                    .Select(d => new { d.Movie.TmdbId, d.Movie.Title, d.WatchedAt, d.LoggedAt, d.Rating, d.Liked, d.Rewatched })
                    .ToListAsync(),
                Ratings = await context.Ratings
                    .Where(r => r.UserId == id)
                    .Select(r => new { r.Movie.TmdbId, r.Movie.Title, r.Score })
                    .ToListAsync(),
                Watched = await context.WatchedMovies
                    .Where(w => w.UserId == id)
                    .Select(w => new { w.Movie.TmdbId, w.Movie.Title, w.WatchedAt, w.Liked, w.Rewatched })
                    .ToListAsync(),
                Watchlist = await context.WatchlistItems
                    .Where(w => w.UserId == id)
                    .OrderBy(w => w.AddedAt)
                    .Select(w => new { w.Movie.TmdbId, w.Movie.Title, w.AddedAt })
                    .ToListAsync(),
                Reviews = await context.Reviews
                    .Where(r => r.UserId == id)
                    .OrderBy(r => r.CreatedAt)
                    .Select(r => new { r.Id, r.Movie.TmdbId, r.Movie.Title, r.Text, r.ContainsSpoilers, r.CreatedAt, r.UpdatedAt })
                    .ToListAsync(),
                Comments = await context.ReviewComments
                    .Where(c => c.UserId == id)
                    .OrderBy(c => c.CreatedAt)
                    .Select(c => new { c.ReviewId, c.Text, c.CreatedAt, c.UpdatedAt })
                    .ToListAsync(),
                LikedReviews = await context.ReviewLikes
                    .Where(l => l.UserId == id)
                    .OrderBy(l => l.CreatedAt)
                    .Select(l => new { l.ReviewId, l.CreatedAt })
                    .ToListAsync(),
                Lists = await context.MovieLists
                    .Where(l => l.UserId == id)
                    .OrderBy(l => l.CreatedAt)
                    .Select(l => new
                    {
                        l.Name,
                        l.Description,
                        l.IsPublic,
                        l.CreatedAt,
                        l.UpdatedAt,
                        Movies = l.Items.OrderBy(i => i.AddedAt).Select(i => new { i.Movie.TmdbId, i.Movie.Title, i.AddedAt }).ToList()
                    })
                    .ToListAsync(),
                Following = await context.Follows
                    .Where(f => f.FollowerId == id)
                    .Select(f => f.Followed.UserName)
                    .ToListAsync(),
                Followers = await context.Follows
                    .Where(f => f.FollowedId == id)
                    .Select(f => f.Follower.UserName)
                    .ToListAsync(),
                Blocked = await context.Blocks
                    .Where(b => b.BlockerId == id)
                    .Select(b => b.Blocked.UserName)
                    .ToListAsync(),
                Reports = await context.Reports
                    .Where(r => r.ReporterId == id)
                    .OrderBy(r => r.CreatedAt)
                    .Select(r => new { r.TargetType, r.TargetId, r.Reason, r.Note, r.CreatedAt, r.ResolvedAt, r.Outcome })
                    .ToListAsync()
            };
        }
    }
}
