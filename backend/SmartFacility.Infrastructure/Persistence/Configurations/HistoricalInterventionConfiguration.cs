using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Infrastructure.Persistence.Configurations;

internal sealed class HistoricalInterventionConfiguration
    : IEntityTypeConfiguration<HistoricalIntervention>
{
    public void Configure(EntityTypeBuilder<HistoricalIntervention> builder)
    {
        builder.ToTable("HistoricalInterventions", "core");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.SourceWorkOrderNumber).HasMaxLength(100).IsRequired();
        builder.Property(item => item.ReportedDateTime).HasColumnType("datetime2").IsRequired();
        builder.Property(item => item.AssetCodeRaw).HasMaxLength(100).IsRequired();
        builder.Property(item => item.WorkOrderStatus).HasMaxLength(200);
        builder.Property(item => item.AssetName).HasMaxLength(1000);
        builder.Property(item => item.CompletionDateTime).HasColumnType("datetime2");
        builder.Property(item => item.RequestDescriptionRaw);
        builder.Property(item => item.RequestDescriptionSanitized);
        builder.Property(item => item.WorkPerformedDescriptionRaw);
        builder.Property(item => item.WorkPerformedDescriptionSanitized);
        builder.Property(item => item.FailureReasonCode).HasMaxLength(100);
        builder.Property(item => item.FailureReasonDescriptionRaw);
        builder.Property(item => item.FailureReasonDescriptionSanitized);
        builder.Property(item => item.MaintenanceDurationRaw).HasMaxLength(100);
        builder.Property(item => item.DowntimeDurationRaw).HasMaxLength(100);
        builder.Property(item => item.LaborDurationRaw).HasMaxLength(100);
        builder.Property(item => item.MaterialCostRaw).HasMaxLength(100);
        builder.Property(item => item.LaborCostRaw).HasMaxLength(100);
        builder.Property(item => item.TotalCostRaw).HasMaxLength(100);
        builder.Property(item => item.TotalCostCurrencyRaw).HasMaxLength(100);
        builder.Property(item => item.InterventionQuality)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(item => item.SourceRowFingerprint)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(item => item.FingerprintAlgorithm)
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(item => item.SourceFileName).HasMaxLength(500).IsRequired();
        builder.Property(item => item.SourceSheet).HasMaxLength(200).IsRequired();
        builder.Property(item => item.ImportedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(item => item.SourceRowFingerprint).IsUnique();
        builder.HasIndex(item => item.WorkOrderId);
        builder.HasIndex(item => item.SourceYear);
        builder.HasIndex(item => item.ReportedDateTime);
        builder.HasIndex(item => new { item.InterventionQuality, item.ReportedDateTime });
        builder.HasIndex(item => item.ImportBatchId);

        builder.HasOne(item => item.WorkOrder)
            .WithMany(item => item.HistoricalInterventions)
            .HasForeignKey(item => item.WorkOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.ImportBatch)
            .WithMany()
            .HasForeignKey(item => item.ImportBatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
