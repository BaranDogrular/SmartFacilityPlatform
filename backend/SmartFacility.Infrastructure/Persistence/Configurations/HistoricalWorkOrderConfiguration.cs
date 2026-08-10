using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Infrastructure.Persistence.Configurations;

internal sealed class HistoricalWorkOrderConfiguration : IEntityTypeConfiguration<HistoricalWorkOrder>
{
    public void Configure(EntityTypeBuilder<HistoricalWorkOrder> builder)
    {
        builder.ToTable("HistoricalWorkOrders", "analytics");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceReference).HasMaxLength(150);
        builder.Property(x => x.ReportedDateTime).HasColumnType("datetime2");
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.Discipline).HasMaxLength(200);
        builder.Property(x => x.PersonnelName).HasMaxLength(500);
        builder.Property(x => x.BuildingNameRaw).HasMaxLength(500);
        builder.Property(x => x.LocationNameRaw).HasMaxLength(1000);
        builder.Property(x => x.ResolutionDurationRaw).HasMaxLength(100);
        builder.Property(x => x.RawData);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.ReportedDateTime);
        builder.HasIndex(x => x.Discipline);
    }
}
