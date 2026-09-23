using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

public sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.HasKey(file => file.Id);
        builder.Property(file => file.Url).HasMaxLength(512).IsRequired();
        builder.HasIndex(file => file.Url).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(file => file.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
