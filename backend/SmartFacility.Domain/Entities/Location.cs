namespace SmartFacility.Domain.Entities;

public sealed class Location
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long BuildingId { get; set; }

    public Building Building { get; set; } = null!;
    public ICollection<Asset> Assets { get; set; } = [];
    public ICollection<WorkOrder> WorkOrders { get; set; } = [];
}
