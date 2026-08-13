namespace SmartFacility.Infrastructure.Configuration;

public sealed class SqlServerOptions
{
    public const string SectionName = "SqlServer";
    public const int DefaultCommandTimeoutSeconds = 30;

    public int CommandTimeoutSeconds { get; set; } = DefaultCommandTimeoutSeconds;
}
