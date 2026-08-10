namespace SmartFacility.Domain.Entities;

public sealed class Asset
{
    public long Id { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? AssetType { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? LastMaintenanceDate { get; set; }
    public string? Status { get; set; }
    public long? ParentAssetId { get; set; }
    public long? BuildingId { get; set; }
    public long? LocationId { get; set; }
    public long? AssetGroupId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Asset? ParentAsset { get; set; }
    public Building? Building { get; set; }
    public Location? Location { get; set; }
    public AssetGroup? AssetGroup { get; set; }
    public ICollection<Asset> ChildAssets { get; set; } = [];
    public ICollection<WorkOrder> WorkOrders { get; set; } = [];
}
