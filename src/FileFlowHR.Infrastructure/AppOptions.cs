namespace FileFlowHR.Infrastructure;

public sealed class AppOptions
{
    public const string Section = "App";

    /// <summary>Base address used in emailed links, for example https://localhost:7180.</summary>
    public string PublicBaseUrl { get; set; } = "https://localhost:7180";

    /// <summary>"SqlServer" (default) or "Sqlite" for laptops without SQL Server.</summary>
    public string DatabaseProvider { get; set; } = "SqlServer";

    /// <summary>Loads Acme and Globex sample data into an empty database.</summary>
    public bool SeedDemoData { get; set; } = true;
}
