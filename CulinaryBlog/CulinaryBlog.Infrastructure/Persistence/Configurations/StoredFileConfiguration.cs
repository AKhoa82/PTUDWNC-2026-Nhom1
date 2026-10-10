using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

public sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.HasKey(file => file.Id);
        builder.Property(file => file.BucketName).HasMaxLength(63);
        builder.Property(file => file.ObjectKey).HasMaxLength(512);
        builder.HasIndex(file => new { file.BucketName, file.ObjectKey }).IsUnique()
            .HasFilter("\"BucketName\" IS NOT NULL AND \"ObjectKey\" IS NOT NULL");
        builder.HasIndex(file => file.UploadExpiresAt).HasFilter("\"Status\" = 0");
        builder.Property(file => file.Url).HasMaxLength(512).IsRequired();
        builder.HasIndex(file => file.Url).IsUnique();
        builder.Property(file => file.DeletionJobId).HasMaxLength(100);
        builder.HasIndex(file => file.DeletionRequestedAt)
            .HasFilter("\"DeletedAt\" IS NULL AND \"DeletionRequestedAt\" IS NOT NULL AND \"DeletionJobId\" IS NULL");
        builder.HasOne<User>().WithMany().HasForeignKey(file => file.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
