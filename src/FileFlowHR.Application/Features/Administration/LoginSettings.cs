using FluentValidation;
using FileFlowHR.Application.Abstractions;
using FileFlowHR.Application.Common;
using FileFlowHR.Application.Common.Messaging;
using FileFlowHR.Application.Common.Security;
using FileFlowHR.Domain.Companies;
using Microsoft.EntityFrameworkCore;

namespace FileFlowHR.Application.Features.Administration;

public sealed record LoginProviderDto(long Id, LoginProviderType Type, string DisplayName, string? Authority, string? ClientId,
    string? SecretName, string? EmailDomain, bool RequireMfa, bool IsEnabled, string SchemeName);

[RequiresPermission(Permissions.SettingsManage)]
public sealed record ListLoginProvidersQuery : IQuery<Result<IReadOnlyList<LoginProviderDto>>>;

public sealed class ListLoginProvidersHandler(IAppDbContext db) : IRequestHandler<ListLoginProvidersQuery, Result<IReadOnlyList<LoginProviderDto>>>
{
    public async Task<Result<IReadOnlyList<LoginProviderDto>>> Handle(ListLoginProvidersQuery request, CancellationToken ct) =>
        (await db.CompanyLoginProviders.AsNoTracking().OrderBy(p => p.ProviderType).ToListAsync(ct))
        .Select(p => new LoginProviderDto(p.Id, p.ProviderType, p.DisplayName, p.Authority, p.ClientId, p.SecretName,
            p.EmailDomain, p.RequireMfa, p.IsEnabled, p.SchemeName)).ToList();
}

[RequiresPermission(Permissions.SettingsManage)]
public sealed record UpdatePasswordLoginCommand(long Id, bool IsEnabled, bool RequireMfa) : ICommand<Result>;

public sealed class UpdatePasswordLoginHandler(IAppDbContext db, ILoginProviderChangeNotifier notifier) : IRequestHandler<UpdatePasswordLoginCommand, Result>
{
    public async Task<Result> Handle(UpdatePasswordLoginCommand request, CancellationToken ct)
    {
        var provider = await db.CompanyLoginProviders.FirstOrDefaultAsync(p => p.Id == request.Id && p.ProviderType == LoginProviderType.Password, ct);
        if (provider is null) return Result.NotFound("Password sign-in setting");

        if (!request.IsEnabled && !await db.CompanyLoginProviders.AnyAsync(p => p.Id != provider.Id && p.IsEnabled, ct))
            return Result.Invalid("Turn on the company login first, or staff will have no way to sign in.");

        provider.SetRequireMfa(request.RequireMfa);
        if (request.IsEnabled) provider.Enable(); else provider.Disable();
        await db.SaveChangesAsync(ct);
        await notifier.ProvidersChangedAsync(ct);
        return Result.Success();
    }
}

/// <summary>Adds or changes the company's own login system (OpenID Connect). The secret stays in Key Vault.</summary>
[RequiresPermission(Permissions.SettingsManage)]
public sealed record SaveOidcLoginCommand(long? Id, string DisplayName, string Authority, string ClientId, string SecretName,
    string? EmailDomain, bool IsEnabled) : ICommand<Result<long>>;

public sealed class SaveOidcLoginValidator : AbstractValidator<SaveOidcLoginCommand>
{
    public SaveOidcLoginValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Authority).NotEmpty().Must(a => a.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Authority must be an https address.");
        RuleFor(x => x.ClientId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SecretName).NotEmpty().MaximumLength(100);
    }
}

public sealed class SaveOidcLoginHandler(IAppDbContext db, ILoginProviderChangeNotifier notifier) : IRequestHandler<SaveOidcLoginCommand, Result<long>>
{
    public async Task<Result<long>> Handle(SaveOidcLoginCommand request, CancellationToken ct)
    {
        CompanyLoginProvider provider;
        if (request.Id is { } id)
        {
            var existing = await db.CompanyLoginProviders.FirstOrDefaultAsync(p => p.Id == id && p.ProviderType == LoginProviderType.Oidc, ct);
            if (existing is null) return Result<long>.NotFound("Company login");
            provider = existing;
            provider.UpdateExternal(request.DisplayName, request.Authority, request.ClientId, request.SecretName, request.EmailDomain);
            if (request.IsEnabled) provider.Enable(); else provider.Disable();
        }
        else
        {
            provider = CompanyLoginProvider.Oidc(request.DisplayName, request.Authority, request.ClientId, request.SecretName,
                request.EmailDomain, request.IsEnabled);
            db.CompanyLoginProviders.Add(provider);
        }
        await db.SaveChangesAsync(ct);
        await notifier.ProvidersChangedAsync(ct);
        return provider.Id;
    }
}
