using Microsoft.EntityFrameworkCore;
using FoodyBackend.Models;


namespace FoodyBackend;

public class DatabaseContext : DbContext
{

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // The connection string should be provided via dependency injection in Program.cs
            // Fallback can be configured here if necessary for design-time migrations, but usually not needed.
            var connectionString =
                $"server=maglev.proxy.rlwy.net;" +
                $"port=3306;" +
                $"database=railway;" +
                $"user=root;" +
                $"password=RmAtHcKSMSnieCvlDeGTFrAnGHvQsIpv;";
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
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

