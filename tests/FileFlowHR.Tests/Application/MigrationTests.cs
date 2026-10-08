using FileFlowHR.Application;
using FileFlowHR.Infrastructure;
using FileFlowHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FileFlowHR.Tests.Application;

public class MigrationTests
{
    /// <summary>
    /// The app's real SQL Server model (built through DI, like at startup) must match the migrations,
    /// otherwise startup fails with PendingModelChangesWarning. No database connection is needed.
    /// </summary>
    [Fact]
    public void Migrations_match_the_runtime_model()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:FileFlowHR"] = "Server=.;Database=FileFlowHR_ModelCheck;Trusted_Connection=True",
            ["App:DatabaseProvider"] = "SqlServer",
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddApplication();
        services.AddInfrastructure(config);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<FileFlowHRDbContext>();
        Assert.False(db.Database.HasPendingModelChanges(), "Run 'dotnet ef migrations add <Name>' to bring the migrations up to date.");
    }
}
