using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Infrastructure.Persistence.Configurations;

internal sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("Assets", "core");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AssetCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(500).IsRequired();
        builder.Property(x => x.AssetType).HasMaxLength(250);
        builder.Property(x => x.SerialNumber).HasMaxLength(250);
        builder.Property(x => x.LastMaintenanceDate).HasColumnType("datetime2");
        builder.Property(x => x.Status).HasMaxLength(150);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.AssetCode).IsUnique();
        builder.HasIndex(x => x.BuildingId);
        builder.HasIndex(x => x.LocationId);
        builder.HasIndex(x => x.AssetGroupId);

        builder.HasOne(x => x.ParentAsset)
            .WithMany(x => x.ChildAssets)
            .HasForeignKey(x => x.ParentAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Building)
            .WithMany(x => x.Assets)
            .HasForeignKey(x => x.BuildingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Location)
            .WithMany(x => x.Assets)
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AssetGroup)
            .WithMany(x => x.Assets)
            .HasForeignKey(x => x.AssetGroupId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
