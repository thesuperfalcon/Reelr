using backend.Features.Auth;
using backend.Features.Diary;
using backend.Features.Moderation;
using backend.Features.MovieLists;
using backend.Features.Movies;
using backend.Features.Ratings;
using backend.Features.Reviews;
using backend.Features.Settings;
using backend.Features.Users;
using backend.Features.WatchedMovies;
using backend.Features.WatchlistItems;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public class ReelrContext : IdentityDbContext<User, IdentityRole<int>, int>, IDataProtectionKeyContext
{
    public ReelrContext(DbContextOptions<ReelrContext> options)
        : base(options)
    {
    }

    public DbSet<Movie> Movies => Set<Movie>();

    public DbSet<Rating> Ratings => Set<Rating>();

    public DbSet<Review> Reviews => Set<Review>();

    public DbSet<ReviewLike> ReviewLikes => Set<ReviewLike>();

    public DbSet<ReviewComment> ReviewComments => Set<ReviewComment>();

    public DbSet<WatchedMovie> WatchedMovies => Set<WatchedMovie>();

    public DbSet<DiaryEntry> DiaryEntries => Set<DiaryEntry>();

    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();

    public DbSet<Follow> Follows => Set<Follow>();

    public DbSet<Block> Blocks => Set<Block>();

    public DbSet<Report> Reports => Set<Report>();

    public DbSet<MovieList> MovieLists => Set<MovieList>();

    public DbSet<MovieListItem> MovieListItems => Set<MovieListItem>();

    public DbSet<UserAvatar> UserAvatars => Set<UserAvatar>();

    // The keys that sign email confirmation and password reset links. Kept in the database so links still
    // work after a restart or a new container, and on every instance.
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .Property(u => u.ProfileImageUrl)
            .HasMaxLength(User.ProfileImageUrlMaxLength);

        // RefreshToken: found by its hash, removed with the user.
        modelBuilder.Entity<RefreshToken>(token =>
        {
            token.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
            token.Property(t => t.TokenHash).HasMaxLength(RefreshToken.HashLength).IsFixedLength().IsUnicode(false);
            token.Property(t => t.SecurityStamp).HasMaxLength(256);
            token.HasIndex(t => t.TokenHash).IsUnique();
            token.HasIndex(t => t.FamilyId);
            token.HasIndex(t => new { t.UserId, t.ExpiresAt });
        });

        // UserAvatar: one per user, removed with the user.
        modelBuilder.Entity<UserAvatar>(avatar =>
        {
            avatar.HasKey(a => a.UserId);
            avatar.HasOne(a => a.User)
                .WithOne()
                .HasForeignKey<UserAvatar>(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            avatar.Property(a => a.ContentType).HasMaxLength(32);
        });

        // Movie
        modelBuilder.Entity<Movie>(movie =>
        {
            movie.HasIndex(m => m.TmdbId).IsUnique();
            movie.HasIndex(m => m.RefreshedAt);
            movie.Property(m => m.Title).HasMaxLength(Movie.TitleMaxLength);
            movie.Property(m => m.Description).HasMaxLength(Movie.DescriptionMaxLength);
            movie.Property(m => m.PosterUrl).HasMaxLength(Movie.ImagePathMaxLength);
            movie.Property(m => m.BackdropUrl).HasMaxLength(Movie.ImagePathMaxLength);
        });

        // WatchedMovie
        modelBuilder.Entity<WatchedMovie>()
            .HasKey(w => new { w.UserId, w.MovieId });

        // DiaryEntry
        modelBuilder.Entity<DiaryEntry>()
            .HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DiaryEntry>()
            .HasOne(d => d.Movie)
            .WithMany()
            .HasForeignKey(d => d.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DiaryEntry>()
            .HasIndex(d => new { d.UserId, d.WatchedAt });

        // The community feed reads the newest logs that are not back-dated; the column is computed and stored,
        // so the filter and the order both come from this index.
        modelBuilder.Entity<DiaryEntry>()
            .Property(d => d.IsBackdated)
            .HasComputedColumnSql(
                $"CAST(CASE WHEN [LoggedAt] > DATEADD(day, {DiaryEntry.BackdatedDays}, [WatchedAt]) THEN 1 ELSE 0 END AS bit)",
                stored: true);

        modelBuilder.Entity<DiaryEntry>()
            .HasIndex(d => new { d.IsBackdated, d.LoggedAt });

        // The following feed: newest logs per user.

        modelBuilder.Entity<DiaryEntry>()
            .HasIndex(d => new { d.UserId, d.LoggedAt });

        modelBuilder.Entity<DiaryEntry>()
            .Property(d => d.Rating)
            .HasPrecision(2, 1);

        modelBuilder.Entity<DiaryEntry>()
            .ToTable(t => t.HasCheckConstraint("CK_DiaryEntries_Rating", $"[Rating] IS NULL OR ({HalfStars("[Rating]")})"));

        // Rating
        modelBuilder.Entity<Rating>()
            .HasKey(r => new { r.UserId, r.MovieId });

        modelBuilder.Entity<Rating>()
            .Property(r => r.Score)
            .HasPrecision(2, 1);

        modelBuilder.Entity<Rating>()
            .ToTable(t => t.HasCheckConstraint("CK_Ratings_Score", HalfStars("[Score]")));

        // WatchlistItem
        modelBuilder.Entity<WatchlistItem>()
            .HasKey(w => new { w.UserId, w.MovieId });

        modelBuilder.Entity<WatchlistItem>()
            .HasIndex(w => new { w.UserId, w.AddedAt });

        // Review
        modelBuilder.Entity<Review>()
            .HasIndex(r => new { r.UserId, r.MovieId })
            .IsUnique();

        // Longer than 4000 characters is stored as nvarchar(max), so a check holds the limit.
        modelBuilder.Entity<Review>()
            .ToTable(t => t.HasCheckConstraint("CK_Reviews_Text", $"LEN([Text]) <= {Review.MaxLength}"));

        // Reviews of a film and reviews by a user, newest first.
        modelBuilder.Entity<Review>()
            .HasIndex(r => new { r.MovieId, r.CreatedAt });

        modelBuilder.Entity<Review>()
            .HasIndex(r => new { r.UserId, r.CreatedAt });

        // The activity feed: newest reviews overall, and newest reviews per user.
        modelBuilder.Entity<Review>()
            .HasIndex(r => r.ActivityAt);

        modelBuilder.Entity<Review>()
            .HasIndex(r => new { r.UserId, r.ActivityAt });

        // No database cascade: SQL Server rejects a second cascade path from users to reviews.
        // JournalService.DeleteEntriesAsync removes the linked review in code instead.
        modelBuilder.Entity<Review>()
            .HasOne(r => r.DiaryEntry)
            .WithOne()
            .HasForeignKey<Review>(r => r.DiaryEntryId)
            .OnDelete(DeleteBehavior.NoAction);

        // ReviewLike and ReviewComment go with their review. The link to the user is Restrict because
        // users already cascade to reviews; UserController.DeleteUser removes them in code.
        modelBuilder.Entity<ReviewLike>(like =>
        {
            like.HasKey(l => new { l.ReviewId, l.UserId });
            like.HasOne(l => l.Review)
                .WithMany(r => r.Likes)
                .HasForeignKey(l => l.ReviewId)
                .OnDelete(DeleteBehavior.Cascade);
            like.HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            like.HasIndex(l => l.UserId);
            like.HasIndex(l => new { l.ReviewId, l.CreatedAt });
        });

        modelBuilder.Entity<ReviewComment>(comment =>
        {
            comment.HasOne(c => c.Review)
                .WithMany(r => r.Comments)
                .HasForeignKey(c => c.ReviewId)
                .OnDelete(DeleteBehavior.Cascade);
            comment.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            comment.Property(c => c.Text).HasMaxLength(ReviewComment.MaxLength);
            comment.HasIndex(c => new { c.ReviewId, c.CreatedAt });
            comment.HasIndex(c => c.UserId);
        });

        // Follow: nobody follows themselves.
        modelBuilder.Entity<Follow>()
            .HasKey(f => new { f.FollowerId, f.FollowedId });

        modelBuilder.Entity<Follow>()
            .ToTable(t => t.HasCheckConstraint("CK_Follows_NotSelf", "[FollowerId] <> [FollowedId]"));

        modelBuilder.Entity<Follow>()
            .HasOne(f => f.Follower)
            .WithMany()
            .HasForeignKey(f => f.FollowerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Follow>()
            .HasOne(f => f.Followed)
            .WithMany()
            .HasForeignKey(f => f.FollowedId)
            .OnDelete(DeleteBehavior.Restrict);

        // Block: like Follow, both links are Restrict and UserController.DeleteUser removes blocks in code.
        modelBuilder.Entity<Block>(block =>
        {
            block.HasKey(b => new { b.BlockerId, b.BlockedId });
            block.HasOne(b => b.Blocker).WithMany().HasForeignKey(b => b.BlockerId).OnDelete(DeleteBehavior.Restrict);
            block.HasOne(b => b.Blocked).WithMany().HasForeignKey(b => b.BlockedId).OnDelete(DeleteBehavior.Restrict);
            block.HasIndex(b => b.BlockedId);
            block.ToTable(t => t.HasCheckConstraint("CK_Blocks_NotSelf", "[BlockerId] <> [BlockedId]"));
        });

        // Report: goes with the reporter. One open report per reporter and item.
        modelBuilder.Entity<Report>(report =>
        {
            report.HasOne(r => r.Reporter).WithMany().HasForeignKey(r => r.ReporterId).OnDelete(DeleteBehavior.Cascade);
            report.Property(r => r.Note).HasMaxLength(Report.NoteMaxLength);
            report.HasIndex(r => new { r.ReporterId, r.TargetType, r.TargetId })
                .IsUnique()
                .HasFilter("[ResolvedAt] IS NULL");
            report.HasIndex(r => new { r.ResolvedAt, r.TargetType, r.TargetId });
        });

        // MovieList
        modelBuilder.Entity<MovieList>(list =>
        {
            list.Property(l => l.Name).HasMaxLength(MovieList.NameMaxLength);
            list.Property(l => l.Description).HasMaxLength(MovieList.DescriptionMaxLength);
        });

        modelBuilder.Entity<MovieList>()
            .HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MovieList>()
            .HasIndex(l => new { l.UserId, l.CreatedAt });

        modelBuilder.Entity<MovieList>()
            .HasIndex(l => new { l.IsPublic, l.CreatedAt });

        // MovieListItem
        modelBuilder.Entity<MovieListItem>()
            .HasKey(i => new { i.MovieListId, i.MovieId });

        modelBuilder.Entity<MovieListItem>()
            .HasOne(i => i.MovieList)
            .WithMany(l => l.Items)
            .HasForeignKey(i => i.MovieListId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MovieListItem>()
            .HasOne(i => i.Movie)
            .WithMany()
            .HasForeignKey(i => i.MovieId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MovieListItem>()
            .HasIndex(i => i.AddedAt);

        // The films of a list, newest addition first.
        modelBuilder.Entity<MovieListItem>()
            .HasIndex(i => new { i.MovieListId, i.AddedAt });
    }

    // A score from half a star to five stars in half steps, the same rule as HalfStarsAttribute.
    private static string HalfStars(string column) =>
        $"{column} >= 0.5 AND {column} <= 5 AND {column} * 2 = FLOOR({column} * 2)";
}
