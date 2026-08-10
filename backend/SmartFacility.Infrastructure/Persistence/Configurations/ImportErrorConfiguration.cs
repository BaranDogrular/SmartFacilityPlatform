using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Infrastructure.Persistence.Configurations;

internal sealed class ImportErrorConfiguration : IEntityTypeConfiguration<ImportError>
{
    public void Configure(EntityTypeBuilder<ImportError> builder)
    {
        builder.ToTable("ImportErrors", "ingestion");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ErrorMessage).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.RawData);
        builder.HasIndex(x => x.ImportBatchId);

        builder.HasOne(x => x.ImportBatch)
            .WithMany(x => x.Errors)
            .HasForeignKey(x => x.ImportBatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
