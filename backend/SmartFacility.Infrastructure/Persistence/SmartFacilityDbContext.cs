using Microsoft.EntityFrameworkCore;
using SmartFacility.Domain.Entities;

namespace SmartFacility.Infrastructure.Persistence;

public sealed class SmartFacilityDbContext(DbContextOptions<SmartFacilityDbContext> options)
    : DbContext(options)
{
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<HistoricalWorkOrder> HistoricalWorkOrders => Set<HistoricalWorkOrder>();
    public DbSet<HistoricalIntervention> HistoricalInterventions => Set<HistoricalIntervention>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<AssetGroup> AssetGroups => Set<AssetGroup>();
    public DbSet<ScadaAlarmEvent> ScadaAlarmEvents => Set<ScadaAlarmEvent>();
    public DbSet<ScadaOutage> ScadaOutages => Set<ScadaOutage>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportError> ImportErrors => Set<ImportError>();
    public DbSet<ImportSourceRecord> ImportSourceRecords => Set<ImportSourceRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmartFacilityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
