using FileFlowHR.Application.Abstractions;
using FileFlowHR.Application.Common;
using FileFlowHR.Application.Common.Messaging;
using FileFlowHR.Domain.Companies;
using Microsoft.EntityFrameworkCore;

namespace FileFlowHR.Application.Features.Companies;

public sealed record ExternalLoginOption(string SchemeName, string DisplayName, string? EmailDomain);
public sealed record LoginOptions(long CompanyId, string CompanyCode, string CompanyName, bool StaffPasswordEnabled,
    IReadOnlyList<ExternalLoginOption> ExternalLogins);

/// <summary>What the sign-in page should offer for a company. Applicants always get email and password.</summary>
[AllowAnonymousRequest]
public sealed record GetLoginOptionsQuery(string CompanyCode) : IQuery<Result<LoginOptions>>;

public sealed class GetLoginOptionsHandler(IAppDbContext db) : IRequestHandler<GetLoginOptionsQuery, Result<LoginOptions>>
{
    public async Task<Result<LoginOptions>> Handle(GetLoginOptionsQuery request, CancellationToken ct)
    {
        var code = request.CompanyCode.Trim().ToLowerInvariant();
        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Code == code && c.Status == CompanyStatus.Active, ct);
        if (company is null) return Result<LoginOptions>.NotFound("Company");

        var providers = await db.CompanyLoginProviders.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.CompanyId == company.Id && p.IsEnabled).ToListAsync(ct);
        return new LoginOptions(company.Id, company.Code, company.LegalName,
            providers.Any(p => p.ProviderType == LoginProviderType.Password),
            providers.Where(p => p.ProviderType == LoginProviderType.Oidc)
                .Select(p => new ExternalLoginOption(p.SchemeName, p.DisplayName, p.EmailDomain)).ToList());
    }
}

/// <summary>Companies whose company login claims the email's domain (for example jane@globex.com finds Globex).</summary>
[AllowAnonymousRequest]
public sealed record FindCompaniesByEmailDomainQuery(string Email) : IQuery<Result<IReadOnlyList<CompanyListItem>>>;

public sealed class FindCompaniesByEmailDomainHandler(IAppDbContext db)
    : IRequestHandler<FindCompaniesByEmailDomainQuery, Result<IReadOnlyList<CompanyListItem>>>
{
    public async Task<Result<IReadOnlyList<CompanyListItem>>> Handle(FindCompaniesByEmailDomainQuery request, CancellationToken ct)
    {
        var at = request.Email.LastIndexOf('@');
        if (at < 0 || at == request.Email.Length - 1) return Result<IReadOnlyList<CompanyListItem>>.Success([]);
        var domain = request.Email[(at + 1)..].Trim().ToLowerInvariant();

        var companyIds = db.CompanyLoginProviders.IgnoreQueryFilters().AsNoTracking()
            .Where(p => p.EmailDomain == domain).Select(p => p.CompanyId);
        return await db.Companies.AsNoTracking()
            .Where(c => c.Status == CompanyStatus.Active && companyIds.Contains(c.Id)).OrderBy(c => c.LegalName)
            .Select(c => new CompanyListItem(c.Code, c.LegalName)).ToListAsync(ct);
    }
}

public sealed record CompanyListItem(string Code, string Name);

/// <summary>Local convenience: lists companies on the sign-in page. A real deployment uses the web address instead.</summary>
[AllowAnonymousRequest]
public sealed record ListCompaniesQuery : IQuery<Result<IReadOnlyList<CompanyListItem>>>;

public sealed class ListCompaniesHandler(IAppDbContext db) : IRequestHandler<ListCompaniesQuery, Result<IReadOnlyList<CompanyListItem>>>
{
    public async Task<Result<IReadOnlyList<CompanyListItem>>> Handle(ListCompaniesQuery request, CancellationToken ct) =>
        await db.Companies.AsNoTracking().Where(c => c.Status == CompanyStatus.Active).OrderBy(c => c.LegalName)
            .Select(c => new CompanyListItem(c.Code, c.LegalName)).ToListAsync(ct);
}
