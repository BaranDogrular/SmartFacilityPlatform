using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Infrastructure.Persistence.Configurations;

internal sealed class ScadaAlarmEventConfiguration : IEntityTypeConfiguration<ScadaAlarmEvent>
{
    public void Configure(EntityTypeBuilder<ScadaAlarmEvent> builder)
    {
        builder.ToTable("ScadaAlarmEvents", "core");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceSheet).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SectionRaw).HasMaxLength(500);
        builder.Property(x => x.LocationRaw).HasMaxLength(1000);
        builder.Property(x => x.FloorRaw).HasMaxLength(200);
        builder.Property(x => x.ZoneRaw).HasMaxLength(500);
        builder.Property(x => x.AlarmType).HasMaxLength(300);
        builder.Property(x => x.InterventionLevel).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.ReceivedAt).HasColumnType("datetime2");
        builder.Property(x => x.ClearedAt).HasColumnType("datetime2");
        builder.Property(x => x.ResponsibleRaw).HasMaxLength(1000);
        builder.Property(x => x.StatusRaw).HasMaxLength(300);
        builder.Property(x => x.Note).HasMaxLength(4000);
        builder.Property(x => x.DateTimeParseStatus).HasMaxLength(100);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.ReceivedAt);
        builder.HasIndex(x => x.AlarmType);
        builder.HasIndex(x => x.SourceSheet);
        builder.HasIndex(x => x.StatusRaw);
    }
}
