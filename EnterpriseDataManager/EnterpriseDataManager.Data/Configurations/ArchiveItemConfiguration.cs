namespace EnterpriseDataManager.Data.Configurations;

using EnterpriseDataManager.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ArchiveItemConfiguration : IEntityTypeConfiguration<ArchiveItem>
{
    public void Configure(EntityTypeBuilder<ArchiveItem> builder)
    {
        builder.ToTable("ArchiveItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourcePath)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.TargetPath)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.SizeBytes)
            .IsRequired();

        builder.Property(x => x.Hash)
            .HasMaxLength(128);

        builder.Property(x => x.Success);

        builder.Property(x => x.Error)
            .HasMaxLength(2000);

        builder.Property(x => x.ProcessedAt);

        builder.HasOne(x => x.ArchiveJob)
            .WithMany(j => j.Items)
            .HasForeignKey(x => x.ArchiveJobId)
            .OnDelete(DeleteBehavior.Cascade);

        ConfigureAuditFields(builder);
        ConfigureSoftDelete(builder);

        builder.Ignore(x => x.DomainEvents);
        builder.Ignore(x => x.IsPending);
        builder.Ignore(x => x.IsProcessed);

        builder.HasIndex(x => x.ArchiveJobId);
        builder.HasIndex(x => x.Success);
        builder.HasIndex(x => x.SourcePath);
    }

    private static void ConfigureAuditFields(EntityTypeBuilder<ArchiveItem> builder)
    {
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedAt);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
    }

    private static void ConfigureSoftDelete(EntityTypeBuilder<ArchiveItem> builder)
    {
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.DeletedAt);
        builder.Property(x => x.DeletedBy).HasMaxLength(256);
    }
}
