namespace EnterpriseDataManager.Data.Configurations;

using EnterpriseDataManager.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class FilePermissionConfiguration : IEntityTypeConfiguration<FilePermission>
{
    public void Configure(EntityTypeBuilder<FilePermission> builder)
    {
        builder.ToTable("FilePermissions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ArchiveItemId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.Level)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.ExpiresAt);

        builder.Property(x => x.SignedUrlToken)
            .HasMaxLength(1024);

        builder.Property(x => x.SignedUrlExpiresAt);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.DeletedAt);
        builder.Property(x => x.DeletedBy).HasMaxLength(256);

        builder.Ignore(x => x.DomainEvents);

        builder.HasIndex(x => x.ArchiveItemId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.ArchiveItemId, x.UserId }).IsUnique();
        builder.HasIndex(x => x.SignedUrlToken);
        builder.HasIndex(x => x.IsDeleted);
    }
}
