using FileFlowHR.Application.Abstractions;
using FileFlowHR.Domain.Companies;
using FileFlowHR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace FileFlowHR.Infrastructure.Authentication;

/// <summary>
/// Each company that uses its own login system (OpenID Connect: Entra ID, Okta, Google Workspace...)
/// becomes an authentication scheme named "oidc-{CompanyLoginProviderId}". Schemes are loaded from
/// the database at start-up and refreshed when an admin changes the settings, without a restart.
/// The client secret is read from configuration "LoginSecrets:{SecretName}" (user secrets locally, Key Vault in Azure).
/// </summary>
public sealed class CompanyOidcSchemes(
    IServiceScopeFactory scopes,
    IAuthenticationSchemeProvider schemeProvider,
    IOptionsMonitorCache<OpenIdConnectOptions> optionsCache,
    IConfiguration configuration,
    ILogger<CompanyOidcSchemes> logger) : ILoginProviderChangeNotifier
{
    private readonly ConcurrentDictionary<string, CompanyLoginProvider> _active = new();

    public CompanyLoginProvider? Find(string scheme) => _active.GetValueOrDefault(scheme);

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FileFlowHRDbContext>();
        var providers = await db.CompanyLoginProviders.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.ProviderType == LoginProviderType.Oidc && p.IsEnabled).ToListAsync(ct);

        foreach (var scheme in _active.Keys.Except(providers.Select(p => p.SchemeName)).ToList())
        {
            schemeProvider.RemoveScheme(scheme);
            optionsCache.TryRemove(scheme);
            _active.TryRemove(scheme, out _);
        }

        foreach (var provider in providers)
        {
            if (string.IsNullOrEmpty(configuration[$"LoginSecrets:{provider.SecretName}"]))
            {
                logger.LogWarning("Company login {Scheme} skipped: secret LoginSecrets:{Secret} is not configured.",
                    provider.SchemeName, provider.SecretName);
                continue;
            }
            _active[provider.SchemeName] = provider;
            optionsCache.TryRemove(provider.SchemeName);
            if (await schemeProvider.GetSchemeAsync(provider.SchemeName) is null)
                schemeProvider.AddScheme(new AuthenticationScheme(provider.SchemeName, provider.DisplayName, typeof(OpenIdConnectHandler)));
        }
    }

    public Task ProvidersChangedAsync(CancellationToken ct) => RefreshAsync(ct);
}

/// <summary>Fills OpenID Connect options for a company scheme from its database row.</summary>
public sealed class ConfigureCompanyOidcOptions(CompanyOidcSchemes schemes, IConfiguration configuration)
    : IConfigureNamedOptions<OpenIdConnectOptions>
{
    public void Configure(string? name, OpenIdConnectOptions options)
    {
        if (name is null || schemes.Find(name) is not { } provider) return;

        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.Authority = provider.Authority;
        options.ClientId = provider.ClientId;
        options.ClientSecret = configuration[$"LoginSecrets:{provider.SecretName}"];
        options.ResponseType = "code";
        options.UsePkce = true;
        options.CallbackPath = $"/signin-{name}";
        options.SignedOutCallbackPath = $"/signout-callback-{name}";
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.SaveTokens = false;
        options.GetClaimsFromUserInfoEndpoint = true;
    }

    public void Configure(OpenIdConnectOptions options) => Configure(Options.DefaultName, options);
}
