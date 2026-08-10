namespace SmartFacility.Domain.Entities;

public sealed class AssetGroup
{
    public long Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;

    public ICollection<Asset> Assets { get; set; } = [];
}
