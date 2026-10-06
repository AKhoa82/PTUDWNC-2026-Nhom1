using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.HasIndex(r => r.Slug).IsUnique();
        builder.HasIndex(r => r.AuthorId);

        builder.HasOne(r => r.Author)
            .WithMany()
            .HasForeignKey(r => r.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(tb => 
        {
            tb.HasCheckConstraint("CK_Recipe_CookingTimeMinutes", "\"CookingTimeMinutes\" >= 0");
            tb.HasCheckConstraint("CK_Recipe_PrepTimeMinutes", "\"PrepTimeMinutes\" > 0");
            tb.HasCheckConstraint("CK_Recipe_Servings", "\"Servings\" > 0");
        });
    }
}
