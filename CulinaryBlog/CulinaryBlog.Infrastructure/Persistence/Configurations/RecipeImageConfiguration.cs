using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

public sealed class RecipeImageConfiguration : IEntityTypeConfiguration<RecipeImage>
{
    public void Configure(EntityTypeBuilder<RecipeImage> builder)
    {
        builder.HasKey(image => image.Id);
        builder.Property(image => image.OriginalUrl).HasMaxLength(500).IsRequired();
        builder.Property(image => image.MediumUrl).HasMaxLength(500);
        builder.Property(image => image.ThumbnailUrl).HasMaxLength(500);
        builder.Property(image => image.AltText).HasMaxLength(200);
        builder.HasOne(image => image.Recipe).WithMany(recipe => recipe.Images)
            .HasForeignKey(image => image.RecipeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(image => image.StoredFile).WithOne()
            .HasForeignKey<RecipeImage>(image => image.StoredFileId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(image => image.StoredFileId).IsUnique();
        builder.HasIndex(image => image.RecipeId)
            .IsUnique().HasFilter("\"IsPrimary\" = TRUE");
        builder.HasIndex(image => new { image.RecipeId, image.OrderIndex });
    }
}
