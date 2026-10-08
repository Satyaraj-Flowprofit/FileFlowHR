using FileFlowHR.Application.Abstractions;
using FileFlowHR.Infrastructure.Authentication;
using FileFlowHR.Infrastructure.Email;
using FileFlowHR.Infrastructure.Identity;
using FileFlowHR.Infrastructure.Persistence;
using FileFlowHR.Infrastructure.Seeding;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace FileFlowHR.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppOptions>(configuration.GetSection(AppOptions.Section));
        var app = configuration.GetSection(AppOptions.Section).Get<AppOptions>() ?? new AppOptions();
        var connectionString = configuration.GetConnectionString("FileFlowHR")
            ?? throw new InvalidOperationException("Connection string 'FileFlowHR' is missing.");

        services.AddScoped<CompanyStampingInterceptor>();
        services.AddDbContext<FileFlowHRDbContext>((sp, options) =>
        {
            if (string.Equals(app.DatabaseProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
                options.UseSqlite(connectionString);
            else
                options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
            options.AddInterceptors(sp.GetRequiredService<CompanyStampingInterceptor>());
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<FileFlowHRDbContext>());

        services.AddIdentityCore<UserAccount>(options =>
            {
                options.SignIn.RequireConfirmedEmail = true;
                options.User.RequireUniqueEmail = false; // the same email may exist at two companies
                options.User.AllowedUserNameCharacters += ":"; // user names are "companycode:email"
                options.Password.RequiredLength = 10;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version2;
            })
            .AddEntityFrameworkStores<FileFlowHRDbContext>()
            .AddSignInManager<HrSignInManager>()
            .AddClaimsPrincipalFactory<HrClaimsPrincipalFactory>()
            .AddDefaultTokenProviders();
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromDays(3));

        services.AddSingleton<DevMailbox>();
        services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<DevMailbox>());
        services.AddSingleton<IEmailSender<UserAccount>, IdentityEmailSender>();
        services.AddScoped<IUserAccountService, UserAccountService>();
        services.AddScoped<ExternalLoginService>();

        services.AddSingleton<CompanyOidcSchemes>();
        services.AddSingleton<ILoginProviderChangeNotifier>(sp => sp.GetRequiredService<CompanyOidcSchemes>());
        services.AddSingleton<IConfigureOptions<OpenIdConnectOptions>, ConfigureCompanyOidcOptions>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<OpenIdConnectOptions>, OpenIdConnectPostConfigureOptions>());

        services.AddScoped<DemoDataSeeder>();
        return services;
    }

    /// <summary>Creates or upgrades the database, loads demo data if asked, and loads company login schemes.</summary>
    public static async Task InitialiseFileFlowHRAsync(this WebApplication app)
    {
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FileFlowHRDbContext>();
            if (db.IsSqlServer) await db.Database.MigrateAsync();
            else await db.Database.EnsureCreatedAsync(); // SQLite: quick local try-out, no migrations

            if (scope.ServiceProvider.GetRequiredService<IOptions<AppOptions>>().Value.SeedDemoData)
                await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
        }
        await app.Services.GetRequiredService<CompanyOidcSchemes>().RefreshAsync();
    }
}
