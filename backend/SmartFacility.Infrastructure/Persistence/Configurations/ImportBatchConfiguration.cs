using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Infrastructure.Persistence.Configurations;

internal sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.ToTable("ImportBatches", "ingestion");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceType).HasMaxLength(200).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(500).IsRequired();
        builder.Property(x => x.StartedAt).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(100).IsRequired();
        builder.Property(x => x.TotalRows).HasDefaultValue(0);
        builder.Property(x => x.SuccessfulRows).HasDefaultValue(0);
        builder.Property(x => x.FailedRows).HasDefaultValue(0);

        builder.HasIndex(x => x.StartedAt);
        builder.HasIndex(x => x.Status);
    }
}
