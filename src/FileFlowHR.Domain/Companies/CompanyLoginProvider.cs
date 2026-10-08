using FileFlowHR.Domain.Common;

namespace FileFlowHR.Domain.Companies;

public enum LoginProviderType { Password, Oidc, Saml }

/// <summary>
/// How a company's staff sign in. "password" means the company has no login system of its own,
/// so FileFlowHR keeps a hashed password. "oidc"/"saml" send staff to the company's own provider.
/// </summary>
public sealed class CompanyLoginProvider : CompanyOwnedEntity
{
    private CompanyLoginProvider() { }

    public LoginProviderType ProviderType { get; private set; }
    public string DisplayName { get; private set; } = "";
    public string? Authority { get; private set; }
    public string? ClientId { get; private set; }
    /// <summary>Name of the client secret in configuration or Key Vault. Never the secret itself.</summary>
    public string? SecretName { get; private set; }
    public string? EmailDomain { get; private set; }
    public bool RequireMfa { get; private set; }
    public bool IsEnabled { get; private set; }

    /// <summary>Authentication scheme name used by ASP.NET Core for external providers.</summary>
    public string SchemeName => $"{ProviderType.ToString().ToLowerInvariant()}-{Id}";

    public static CompanyLoginProvider Password(bool requireMfa) => new()
    {
        ProviderType = LoginProviderType.Password,
        DisplayName = "Email and password",
        RequireMfa = requireMfa,
        IsEnabled = true,
    };

    public static CompanyLoginProvider Oidc(string displayName, string authority, string clientId,
        string secretName, string? emailDomain, bool isEnabled)
    {
        var provider = new CompanyLoginProvider { ProviderType = LoginProviderType.Oidc };
        provider.UpdateExternal(displayName, authority, clientId, secretName, emailDomain);
        provider.IsEnabled = isEnabled;
        return provider;
    }

    public void UpdateExternal(string displayName, string authority, string clientId, string secretName, string? emailDomain)
    {
        if (ProviderType == LoginProviderType.Password)
            throw new DomainException("Password sign-in has no external settings.");
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new DomainException("Authority must be an https address.");

        DisplayName = Guard.Required(displayName, "Display name", 100);
        Authority = authority.Trim();
        ClientId = Guard.Required(clientId, "Client ID", 200);
        SecretName = Guard.Required(secretName, "Secret name", 100);
        EmailDomain = Guard.Optional(emailDomain, "Email domain", 253)?.ToLowerInvariant();
    }

    public void SetRequireMfa(bool value)
    {
        if (ProviderType != LoginProviderType.Password)
            throw new DomainException("MFA for company sign-in is set in the company's own login system.");
        RequireMfa = value;
    }

    public void Enable() => IsEnabled = true;
    public void Disable() => IsEnabled = false;
}
