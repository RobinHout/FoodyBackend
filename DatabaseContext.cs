using FoodyBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace FoodyBackend;

public class DatabaseContext(DbContextOptions<DatabaseContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Dinner> Dinners => Set<Dinner>();
    public DbSet<DinnerParticipation> DinnerParticipations => Set<DinnerParticipation>();
    public DbSet<DinnerRecipeOption> DinnerRecipeOptions => Set<DinnerRecipeOption>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<RecipeLabel> RecipeLabels => Set<RecipeLabel>();
    public DbSet<UserGroup> UserGroups => Set<UserGroup>();
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

        modelBuilder.Entity<DinnerParticipation>()
            .HasIndex(participation => new { participation.DinnerId, participation.UserId })
            .IsUnique();

        modelBuilder.Entity<DinnerParticipation>()
            .HasOne(participation => participation.Dinner)
            .WithMany()
            .HasForeignKey(participation => participation.DinnerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DinnerParticipation>()
            .HasOne(participation => participation.User)
            .WithMany()
            .HasForeignKey(participation => participation.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DinnerRecipeOption>()
            .HasIndex(option => new { option.DinnerId, option.Rank })
            .IsUnique();

        modelBuilder.Entity<DinnerRecipeOption>()
            .HasOne(option => option.Dinner)
            .WithMany()
            .HasForeignKey(option => option.DinnerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DinnerRecipeOption>()
            .HasOne(option => option.Recipe)
            .WithMany()
            .HasForeignKey(option => option.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AuthSession>()
            .HasIndex(session => session.AccessTokenHash)
            .IsUnique();

        modelBuilder.Entity<AuthSession>()
            .HasIndex(session => session.RefreshTokenHash)
            .IsUnique();

        modelBuilder.Entity<AuthSession>()
            .HasOne(session => session.User)
            .WithMany()
            .HasForeignKey(session => session.UserId)
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

        modelBuilder.Entity<UserGroup>()
            .HasIndex(userGroup => new { userGroup.UserId, userGroup.GroupId })
            .IsUnique();

        modelBuilder.Entity<UserGroup>()
            .HasOne(userGroup => userGroup.User)
            .WithMany()
            .HasForeignKey(userGroup => userGroup.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserGroup>()
            .HasOne(userGroup => userGroup.Group)
            .WithMany()
            .HasForeignKey(userGroup => userGroup.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserLabel>()
            .HasIndex(userLabel => new { userLabel.UserId, userLabel.LabelId })
            .IsUnique();

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
