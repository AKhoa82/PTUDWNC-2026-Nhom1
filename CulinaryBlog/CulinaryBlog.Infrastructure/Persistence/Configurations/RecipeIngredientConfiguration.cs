using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

public class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        builder.HasKey(ri => ri.Id);

        builder.Property(ri => ri.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ri => ri.Quantity)
            .HasColumnType("decimal(10,3)");

        builder.Property(ri => ri.Unit)
            .HasMaxLength(50);

        builder.Property(ri => ri.Notes)
            .HasMaxLength(200);

        builder.Property(ri => ri.SortOrder)
            .IsRequired()
            .HasDefaultValue(0);
            
        builder.HasOne(ri => ri.Recipe)
            .WithMany(r => r.Ingredients)
            .HasForeignKey(ri => ri.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
