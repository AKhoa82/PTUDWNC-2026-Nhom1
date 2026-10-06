using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CulinaryBlog.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.Property(r => r.TokenHash)
            .IsRequired()
            .HasMaxLength(64)
            .HasColumnType("varchar(64)");
            
        builder.HasIndex(r => r.TokenHash).IsUnique();

        builder.Property(r => r.ReplacedByTokenHash)
            .HasMaxLength(64)
            .HasColumnType("varchar(64)");

        builder.Property(r => r.CreatedByIp)
            .HasMaxLength(45)
            .HasColumnType("varchar(45)");
    }
}
