using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<RecipeCacheInvalidation> RecipeCacheInvalidations => Set<RecipeCacheInvalidation>();
    public new DbSet<User> Users => Set<User>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeImage> RecipeImages => Set<RecipeImage>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Legacy create/seed callers still supply ImageUrl; keep their gallery consistent too.
        foreach (var entry in ChangeTracker.Entries<Recipe>().Where(entry => entry.State == EntityState.Added).ToList())
        {
            var recipe = entry.Entity;
            if (recipe.Images.Count == 0 && !string.IsNullOrWhiteSpace(recipe.ImageUrl))
                recipe.Images.Add(new RecipeImage { RecipeId = recipe.Id, OriginalUrl = recipe.ImageUrl, IsPrimary = true });
        }
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<RecipeCacheInvalidation>().HasIndex(x => x.CreatedAt).HasFilter("\"ProcessedAt\" IS NULL");
        modelBuilder.HasAnnotation("CulinaryBlog:IdentityBackfill", "1");
        modelBuilder.Entity<User>(user =>
        {
            user.Property(entity => entity.UserName).HasColumnName("Username");
            user.HasIndex(entity => entity.Email).IsUnique();
            user.HasIndex(entity => entity.UserName).IsUnique();
        });

        modelBuilder.Entity<RefreshToken>().HasIndex(token => token.Token).IsUnique();
        modelBuilder.Entity<RefreshToken>()
            .HasOne(token => token.User)
            .WithMany()
            .HasForeignKey(token => token.UserId);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
