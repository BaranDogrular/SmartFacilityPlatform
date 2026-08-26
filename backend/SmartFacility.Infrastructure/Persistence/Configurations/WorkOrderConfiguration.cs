using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Infrastructure.Persistence.Configurations;

internal sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("WorkOrders", "core");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.WorkOrderNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ReportedDateTime).HasColumnType("datetime2");
        builder.Property(x => x.AssetCodeRaw).HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.Discipline).HasMaxLength(200);
        builder.Property(x => x.RequestedByName).HasMaxLength(300);
        builder.Property(x => x.AssignedPersonnelName).HasMaxLength(500);
        builder.Property(x => x.Status).HasMaxLength(200);
        builder.Property(x => x.WorkType).HasMaxLength(200);
        builder.Property(x => x.FailureType).HasMaxLength(250);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.LocationNameRaw).HasMaxLength(500);
        builder.Property(x => x.ResponseDurationRaw).HasMaxLength(100);
        builder.Property(x => x.DowntimeRaw).HasMaxLength(100);
        builder.Property(x => x.MaintenanceDurationRaw).HasMaxLength(100);
        builder.Property(x => x.TotalCostRaw).HasMaxLength(100);
        builder.Property(x => x.ServiceCostRaw).HasMaxLength(100);
        builder.Property(x => x.RawStatusCode).HasMaxLength(100);
        builder.Property(x => x.CanonicalIdentityFingerprint).HasMaxLength(64).IsUnicode(false);
        builder.Property(x => x.SourceRowFingerprint).HasMaxLength(64).IsUnicode(false);
        builder.Property(x => x.IsInCanonicalSnapshot).HasDefaultValue(true);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.WorkOrderNumber);
        builder.HasIndex(x => x.ReportedDateTime);
        builder.HasIndex(x => x.AssetId);
        builder.HasIndex(x => x.AssetCodeRaw);
        builder.HasIndex(x => x.BuildingId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.RawStatusCode);
        builder.HasIndex(x => x.CanonicalIdentityFingerprint);
        builder.HasIndex(x => x.IsInCanonicalSnapshot);

        builder.HasOne(x => x.Asset)
            .WithMany(x => x.WorkOrders)
            .HasForeignKey(x => x.AssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Building)
            .WithMany(x => x.WorkOrders)
            .HasForeignKey(x => x.BuildingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Location)
            .WithMany(x => x.WorkOrders)
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.LastSeenImportBatch)
            .WithMany()
            .HasForeignKey(x => x.LastSeenImportBatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
