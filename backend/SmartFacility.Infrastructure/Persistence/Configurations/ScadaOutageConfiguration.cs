using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Infrastructure.Persistence.Configurations;

internal sealed class ScadaOutageConfiguration : IEntityTypeConfiguration<ScadaOutage>
{
    public void Configure(EntityTypeBuilder<ScadaOutage> builder)
    {
        builder.ToTable("ScadaOutages", "core");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceSheet).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.StartedAt).HasColumnType("datetime2");
        builder.Property(x => x.RestoredAt).HasColumnType("datetime2");
        builder.Property(x => x.DurationRaw).HasMaxLength(100);
        builder.Property(x => x.StatusRaw).HasMaxLength(300);
        builder.Property(x => x.DateTimeParseStatus).HasMaxLength(100);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.StartedAt);
        builder.HasIndex(x => x.StatusRaw);
    }
}
