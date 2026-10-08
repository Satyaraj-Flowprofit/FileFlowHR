using System.Security.Claims;
using FileFlowHR.Application;
using FileFlowHR.Application.Abstractions;
using FileFlowHR.Application.Common;
using FileFlowHR.Application.Common.Messaging;
using FileFlowHR.Infrastructure;
using FileFlowHR.Infrastructure.Identity;
using FileFlowHR.Infrastructure.Persistence;
using FileFlowHR.Infrastructure.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FileFlowHR.Tests;

/// <summary>
/// The real Application and Infrastructure wiring on an in-memory SQLite database with the Acme and
/// Globex demo data. Tests sign in as a demo user and send use cases through the dispatcher, exactly as pages do.
/// </summary>
public sealed class TestApp : IAsyncDisposable
{
    private readonly SqliteConnection _keepAlive;
    private readonly ServiceProvider _services;
    private readonly TestPrincipal _principal;

    private TestApp(SqliteConnection keepAlive, ServiceProvider services, TestPrincipal principal)
    {
        _keepAlive = keepAlive;
        _services = services;
        _principal = principal;
    }

    public static async Task<TestApp> CreateAsync()
    {
        var connectionString = $"Data Source=hr-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync(); // the in-memory database lives while one connection is open

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:FileFlowHR"] = connectionString,
            ["App:DatabaseProvider"] = "Sqlite",
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddApplication();
        services.AddInfrastructure(config);
        var principal = new TestPrincipal();
        services.AddSingleton<IPrincipalAccessor>(principal);

        var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<FileFlowHRDbContext>().Database.EnsureCreatedAsync();
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
        }
        return new TestApp(keepAlive, provider, principal);
    }

    public async Task SignInAsync(string company, string email)
    {
        await using var scope = _services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<UserAccount>>();
        var user = await users.FindByNameAsync(UserAccount.MakeUserName(company, email))
                   ?? throw new InvalidOperationException($"No demo login {company}:{email}");
        var factory = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<UserAccount>>();
        _principal.Current = await factory.CreateAsync(user);
    }

    public void SignOut() => _principal.Current = new ClaimsPrincipal(new ClaimsIdentity());

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request) where TResponse : IResult, IResultFactory<TResponse> =>
        _services.GetRequiredService<IDispatcher>().Send(request);

    /// <summary>Direct database access for checks, as a given company.</summary>
    public async Task<T> QueryAsync<T>(Func<FileFlowHRDbContext, Task<T>> query)
    {
        await using var scope = _services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<FileFlowHR.Application.Common.Security.UserContext>().SetUser(_principal.Current);
        return await query(scope.ServiceProvider.GetRequiredService<FileFlowHRDbContext>());
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _keepAlive.DisposeAsync();
    }

    private sealed class TestPrincipal : IPrincipalAccessor
    {
        public ClaimsPrincipal Current { get; set; } = new(new ClaimsIdentity());
        public Task<ClaimsPrincipal> GetPrincipalAsync() => Task.FromResult(Current);
    }
}
