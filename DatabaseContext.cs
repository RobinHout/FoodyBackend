using FoodyBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace FoodyBackend;

public class DatabaseContext(DbContextOptions<DatabaseContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Dinner> Dinners => Set<Dinner>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<RecipeLabel> RecipeLabels => Set<RecipeLabel>();
    public DbSet<UserLabel> UserLabels => Set<UserLabel>();
    public DbSet<Answers> Answers => Set<Answers>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Dinner>()
            .HasOne(dinner => dinner.Group)
            .WithMany()
            .HasForeignKey(dinner => dinner.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Answers>()
            .HasOne(answer => answer.Dinner)
            .WithMany()
            .HasForeignKey(answer => answer.DinnerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Answers>()
            .HasOne(answer => answer.User)
            .WithMany()
            .HasForeignKey(answer => answer.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RecipeLabel>()
            .HasOne(recipeLabel => recipeLabel.Recipe)
            .WithMany()
            .HasForeignKey(recipeLabel => recipeLabel.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RecipeLabel>()
            .HasOne(recipeLabel => recipeLabel.Label)
            .WithMany()
            .HasForeignKey(recipeLabel => recipeLabel.LabelId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserLabel>()
            .HasOne(userLabel => userLabel.User)
            .WithMany()
            .HasForeignKey(userLabel => userLabel.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserLabel>()
            .HasOne(userLabel => userLabel.Label)
            .WithMany()
            .HasForeignKey(userLabel => userLabel.LabelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

