using backend.Features.Diary;
using backend.Features.MovieLists;
using backend.Features.Movies;
using backend.Features.Ratings;
using backend.Features.Reviews;
using backend.Features.Settings;
using backend.Features.Users;
using backend.Features.WatchedMovies;
using backend.Features.WatchlistItems;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public class ReelrContext : IdentityDbContext<User, IdentityRole<int>, int>
{
    public ReelrContext(DbContextOptions<ReelrContext> options)
        : base(options)
    {
    }

    public DbSet<Movie> Movies => Set<Movie>();

    public DbSet<Rating> Ratings => Set<Rating>();

    public DbSet<Review> Reviews => Set<Review>();

    public DbSet<WatchedMovie> WatchedMovies => Set<WatchedMovie>();

    public DbSet<DiaryEntry> DiaryEntries => Set<DiaryEntry>();

    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();

    public DbSet<Follow> Follows => Set<Follow>();

    public DbSet<MovieList> MovieLists => Set<MovieList>();

    public DbSet<MovieListItem> MovieListItems => Set<MovieListItem>();

    public DbSet<UserAvatar> UserAvatars => Set<UserAvatar>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
        modelBuilder.Entity<Movie>()
            .HasIndex(m => m.TmdbId)
            .IsUnique();

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

        // Activity feed: newest logs overall, and newest logs per user.
        modelBuilder.Entity<DiaryEntry>()
            .HasIndex(d => d.LoggedAt);

        modelBuilder.Entity<DiaryEntry>()
            .HasIndex(d => new { d.UserId, d.LoggedAt });

        modelBuilder.Entity<DiaryEntry>()
            .Property(d => d.Rating)
            .HasPrecision(2, 1);

        // Rating
        modelBuilder.Entity<Rating>()
            .HasKey(r => new { r.UserId, r.MovieId });

        modelBuilder.Entity<Rating>()
            .Property(r => r.Score)
            .HasPrecision(2, 1);

        // WatchlistItem
        modelBuilder.Entity<WatchlistItem>()
            .HasKey(w => new { w.UserId, w.MovieId });

        modelBuilder.Entity<WatchlistItem>()
            .HasIndex(w => new { w.UserId, w.AddedAt });

        // Review
        modelBuilder.Entity<Review>()
            .HasIndex(r => new { r.UserId, r.MovieId })
            .IsUnique();

        modelBuilder.Entity<Review>()
            .HasIndex(r => r.CreatedAt);

        modelBuilder.Entity<Review>()
            .HasIndex(r => new { r.UserId, r.CreatedAt });

        // No database cascade: SQL Server rejects a second cascade path from users to reviews.
        // DiaryExtensions.DeleteDiaryEntriesAsync removes the linked review in code instead.
        modelBuilder.Entity<Review>()
            .HasOne(r => r.DiaryEntry)
            .WithOne()
            .HasForeignKey<Review>(r => r.DiaryEntryId)
            .OnDelete(DeleteBehavior.NoAction);

        // Follow
        modelBuilder.Entity<Follow>()
            .HasKey(f => new { f.FollowerId, f.FollowedId });

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

        // MovieList
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
    }
}
