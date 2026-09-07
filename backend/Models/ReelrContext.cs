using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace backend.Models
{
    public class ReelrContext : DbContext
    {
        public ReelrContext(DbContextOptions<ReelrContext> options) : base(options)
        {
        }
        public DbSet<Movie> Movies { get; set; } = null!;
    }
}
