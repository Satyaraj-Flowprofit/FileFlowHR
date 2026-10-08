using FluentValidation;
using FileFlowHR.Application.Abstractions;
using FileFlowHR.Application.Common;
using FileFlowHR.Application.Common.Messaging;
using FileFlowHR.Application.Common.Security;
using FileFlowHR.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace FileFlowHR.Application.Features.Administration;

public sealed record RoleGrantRow(long Id, long PersonId, string PersonName, GrantableRole Role, string RoleName, ScopeType ScopeType,
    string ScopeName, DateOnly From, DateOnly? To, string? GrantedByName);

[RequiresPermission(Permissions.RolesManage)]
public sealed record ListRoleGrantsQuery(bool IncludeEnded = false) : IQuery<Result<IReadOnlyList<RoleGrantRow>>>;

public sealed class ListRoleGrantsHandler(IAppDbContext db, TimeProvider clock) : IRequestHandler<ListRoleGrantsQuery, Result<IReadOnlyList<RoleGrantRow>>>
{
    public async Task<Result<IReadOnlyList<RoleGrantRow>>> Handle(ListRoleGrantsQuery request, CancellationToken ct)
    {
        var today = clock.Today();
        var grants = db.RoleGrants.AsNoTracking();
        if (!request.IncludeEnded) grants = grants.Where(g => g.EffectiveTo == null || g.EffectiveTo > today);
        var list = await grants.ToListAsync(ct);

        var names = await db.People.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.FirstName + " " + p.LastName, ct);
        var regions = await db.Regions.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name, ct);
        var units = await db.BusinessUnits.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name, ct);

        string Scope(RoleGrant g) => g.ScopeType switch
        {
            ScopeType.Company => "Whole company",
            ScopeType.Region => $"Region: {regions.GetValueOrDefault(g.ScopeId ?? 0, "?")}",
            _ => $"Business unit: {units.GetValueOrDefault(g.ScopeId ?? 0, "?")}",
        };

        return list.OrderBy(g => names.GetValueOrDefault(g.PersonId)).ThenBy(g => g.RoleCode)
            .Select(g => new RoleGrantRow(g.Id, g.PersonId, names.GetValueOrDefault(g.PersonId, "?"), g.RoleCode,
                Roles.DisplayName(RoleAssignment.RoleCode(g.RoleCode)), g.ScopeType, Scope(g), g.EffectiveFrom, g.EffectiveTo,
                g.GrantedBy is { } by ? names.GetValueOrDefault(by) : "Set up at onboarding"))
            .ToList();
    }
}

[RequiresPermission(Permissions.RolesManage)]
public sealed record GrantRoleCommand(long PersonId, GrantableRole Role, ScopeType ScopeType, long? ScopeId, DateOnly From, DateOnly? To)
    : ICommand<Result<long>>;

public sealed class GrantRoleValidator : AbstractValidator<GrantRoleCommand>
{
    public GrantRoleValidator()
    {
        RuleFor(x => x.PersonId).GreaterThan(0).WithMessage("Choose a person.");
        RuleFor(x => x.ScopeId).NotNull().When(x => x.ScopeType != ScopeType.Company)
            .WithMessage("Choose the region or business unit this role covers.");
        RuleFor(x => x).Must(x => x.To == null || x.To > x.From).WithMessage("The end date must be after the start date.");
        RuleFor(x => x).Must(x => x.Role != GrantableRole.Recruiter || x.ScopeType == ScopeType.BusinessUnit)
            .WithMessage("A Recruiter is always given for one business unit.");
    }
}

public sealed class GrantRoleHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock) : IRequestHandler<GrantRoleCommand, Result<long>>
{
    public async Task<Result<long>> Handle(GrantRoleCommand request, CancellationToken ct)
    {
        var today = clock.Today();
        if (!await db.Employments.AnyAsync(e => e.PersonId == request.PersonId && (e.TerminationDate == null || e.TerminationDate > today), ct))
            return Result<long>.Invalid("Roles can only be given to current employees.");
        if (request.ScopeType == ScopeType.Region && !await db.Regions.AnyAsync(r => r.Id == request.ScopeId, ct))
            return Result<long>.NotFound("Region");
        if (request.ScopeType == ScopeType.BusinessUnit && !await db.BusinessUnits.AnyAsync(b => b.Id == request.ScopeId, ct))
            return Result<long>.NotFound("Business unit");
        if (await db.RoleGrants.AnyAsync(g => g.PersonId == request.PersonId && g.RoleCode == request.Role && g.ScopeType == request.ScopeType
                                             && g.ScopeId == request.ScopeId && (g.EffectiveTo == null || g.EffectiveTo > request.From), ct))
            return Result<long>.Invalid("This person already has that role for that area.");

        var grant = RoleGrant.Create(request.PersonId, request.Role, request.ScopeType,
            request.ScopeType == ScopeType.Company ? null : request.ScopeId, request.From, request.To, user.PersonId);
        db.RoleGrants.Add(grant);
        await db.SaveChangesAsync(ct);
        return grant.Id;
    }
}

[RequiresPermission(Permissions.RolesManage)]
public sealed record EndRoleGrantCommand(long RoleGrantId, DateOnly EndDate) : ICommand<Result>;

public sealed class EndRoleGrantHandler(IAppDbContext db, ICurrentUser user) : IRequestHandler<EndRoleGrantCommand, Result>
{
    public async Task<Result> Handle(EndRoleGrantCommand request, CancellationToken ct)
    {
        var grant = await db.RoleGrants.FindAsync([request.RoleGrantId], ct);
        if (grant is null) return Result.NotFound("Role");
        if (grant.PersonId == user.PersonId && grant.RoleCode == GrantableRole.ClientAdmin)
            return Result.Invalid("You cannot remove your own Client Admin role. Ask another Client Admin.");
        grant.EndOn(request.EndDate);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
