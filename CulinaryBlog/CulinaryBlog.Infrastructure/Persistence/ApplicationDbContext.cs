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

    private bool ChangesRecipeList() => ChangeTracker.Entries().Any(entry =>
        (entry.Entity is Recipe || entry.Entity is Category) &&
        entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        UpdateRecipeListVersion();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => SaveChangesAsync(true, cancellationToken);

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        UpdateRecipeListVersion();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void UpdateRecipeListVersion()
    {
        if (!ChangesRecipeList()) return;
        var version = Set<RecipeListVersion>().Local.SingleOrDefault();
        if (version is null)
        {
            version = new RecipeListVersion { Id = 1 };
            Attach(version);
        }
        version.Version = Guid.NewGuid().ToString("N");
        Entry(version).Property(value => value.Version).IsModified = true;
    }

    // Always read the committed database generation, never a Redis copy or tracked entity.
    public Task<string?> GetRecipeListVersionAsync(CancellationToken cancellationToken = default)
        => Set<RecipeListVersion>().AsNoTracking().Where(value => value.Id == 1)
            .Select(value => (string?)value.Version).SingleOrDefaultAsync(cancellationToken);

    public DbSet<Category> Categories => Set<Category>();
    public new DbSet<User> Users => Set<User>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<RecipeListVersion>(version =>
        {
            version.ToTable("RecipeListVersions");
            version.HasKey(value => value.Id);
            version.Property(value => value.Id).ValueGeneratedNever();
            version.Property(value => value.Version).HasMaxLength(32).IsRequired();
            version.HasData(new RecipeListVersion { Id = 1, Version = "initial" });
        });
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

internal sealed class RecipeListVersion
{
    public int Id { get; set; }
    public string Version { get; set; } = string.Empty;
}
