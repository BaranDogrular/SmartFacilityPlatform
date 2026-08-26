namespace SmartFacility.Domain;

public static class WorkOrderSourceState
{
    public const string Open = "A";
    public const string Closed = "K";

    public static bool IsOpen(string? rawStatusCode) =>
        string.Equals(rawStatusCode?.Trim(), Open, StringComparison.OrdinalIgnoreCase);

    public static bool IsClosed(string? rawStatusCode) =>
        string.Equals(rawStatusCode?.Trim(), Closed, StringComparison.OrdinalIgnoreCase);
}
