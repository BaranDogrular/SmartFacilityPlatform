namespace SmartFacility.Domain.Entities;

public sealed class Building
{
    public long Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Location> Locations { get; set; } = [];
    public ICollection<Asset> Assets { get; set; } = [];
    public ICollection<WorkOrder> WorkOrders { get; set; } = [];
}
