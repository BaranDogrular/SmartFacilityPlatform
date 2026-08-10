using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Infrastructure.Persistence.Configurations;

internal sealed class ImportSourceRecordConfiguration : IEntityTypeConfiguration<ImportSourceRecord>
{
    public void Configure(EntityTypeBuilder<ImportSourceRecord> builder)
    {
        builder.ToTable("ImportSourceRecords", "ingestion");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceSheet).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RawData).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.RawFormulaData).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ParseStatus).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => new { x.ImportBatchId, x.SourceSheet, x.SourceRowNumber });

        builder.HasOne(x => x.ImportBatch)
            .WithMany(x => x.SourceRecords)
            .HasForeignKey(x => x.ImportBatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
