using backend.Features.MovieLists;
using backend.Features.Movies;
using backend.Features.Ratings;
using backend.Features.Reviews;
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

    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();

    public DbSet<Follow> Follows => Set<Follow>();

    public DbSet<MovieList> MovieLists => Set<MovieList>();

    public DbSet<MovieListItem> MovieListItems => Set<MovieListItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Movie
        modelBuilder.Entity<Movie>()
            .HasIndex(m => m.TmdbId)
            .IsUnique();

        // WatchedMovie
        modelBuilder.Entity<WatchedMovie>()
            .HasKey(w => new { w.UserId, w.MovieId });

        // Rating
        modelBuilder.Entity<Rating>()
            .HasKey(r => new { r.UserId, r.MovieId });

        modelBuilder.Entity<Rating>()
            .Property(r => r.Score)
            .HasPrecision(2, 1);

        // WatchlistItem
        modelBuilder.Entity<WatchlistItem>()
            .HasKey(w => new { w.UserId, w.MovieId });

        // Review
        modelBuilder.Entity<Review>()
            .HasIndex(r => new { r.UserId, r.MovieId })
            .IsUnique();

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
    }
}
