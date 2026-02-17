using Microsoft.EntityFrameworkCore;
using FoodyBackend.Models;

namespace FoodyBackend
{
    public class DatabaseContext : DbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }

        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var connectionString = "postgresql://postgres:QmTsxuMoGQBuRrKTSxkwiBpFiqbjmJVp@postgres.railway.internal:5432/railway";
                optionsBuilder.UseNpgsql(connectionString);
            }
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<Dinner> Dinners { get; set; }
        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<Label> Labels { get; set; }
        public DbSet<RecipeLabel> RecipeLabels { get; set; }
        public DbSet<UserLabel> UserLabels { get; set; }
        public DbSet<Answers> Answers { get; set; }
    }
}