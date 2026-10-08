using FluentValidation;
using FileFlowHR.Application.Abstractions;
using FileFlowHR.Application.Common;
using FileFlowHR.Application.Common.Messaging;
using FileFlowHR.Application.Common.Security;
using FileFlowHR.Domain.Organisation;
using Microsoft.EntityFrameworkCore;

namespace FileFlowHR.Application.Features.Organisation;

public sealed record BusinessUnitDto(long Id, long RegionId, string RegionName, long? ParentBusinessUnitId,
    string? ParentName, string Name, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

[RequiresPermission(Permissions.OrgView)]
public sealed record ListBusinessUnitsQuery : IQuery<Result<IReadOnlyList<BusinessUnitDto>>>;

public sealed class ListBusinessUnitsHandler(IAppDbContext db)
    : IRequestHandler<ListBusinessUnitsQuery, Result<IReadOnlyList<BusinessUnitDto>>>
{
    public async Task<Result<IReadOnlyList<BusinessUnitDto>>> Handle(ListBusinessUnitsQuery request, CancellationToken ct) =>
        await (from b in db.BusinessUnits.AsNoTracking()
               join r in db.Regions on b.RegionId equals r.Id
               join p in db.BusinessUnits on b.ParentBusinessUnitId equals p.Id into parents
               from p in parents.DefaultIfEmpty()
               orderby r.Name, b.Name
               select new BusinessUnitDto(b.Id, b.RegionId, r.Name, b.ParentBusinessUnitId, p != null ? p.Name : null,
                   b.Name, b.EffectiveFrom, b.EffectiveTo))
            .ToListAsync(ct);
}

[RequiresPermission(Permissions.OrgManage)]
public sealed record SaveBusinessUnitCommand(long? Id, long RegionId, long? ParentBusinessUnitId, string Name, DateOnly EffectiveFrom)
    : ICommand<Result<long>>;

public sealed class SaveBusinessUnitValidator : AbstractValidator<SaveBusinessUnitCommand>
{
    public SaveBusinessUnitValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.RegionId).GreaterThan(0).WithMessage("Choose a region.");
    }
}

public sealed class SaveBusinessUnitHandler(IAppDbContext db) : IRequestHandler<SaveBusinessUnitCommand, Result<long>>
{
    public async Task<Result<long>> Handle(SaveBusinessUnitCommand request, CancellationToken ct)
    {
        if (!await db.Regions.AnyAsync(r => r.Id == request.RegionId, ct))
            return Result<long>.NotFound("Region");
        if (request.ParentBusinessUnitId is { } parentId && !await db.BusinessUnits.AnyAsync(b => b.Id == parentId, ct))
            return Result<long>.NotFound("Parent business unit");

        BusinessUnit unit;
        if (request.Id is { } id)
        {
            var existing = await db.BusinessUnits.FindAsync([id], ct);
            if (existing is null) return Result<long>.NotFound("Business unit");
            unit = existing;
            if (request.ParentBusinessUnitId is { } newParent && await IsBelow(newParent, id, ct))
                return Result<long>.Invalid("A business unit cannot sit under one of its own sub-units.");
            unit.Update(request.ParentBusinessUnitId, request.Name);
        }
        else
        {
            unit = BusinessUnit.Create(request.RegionId, request.ParentBusinessUnitId, request.Name, request.EffectiveFrom);
            db.BusinessUnits.Add(unit);
        }
        await db.SaveChangesAsync(ct);
        return unit.Id;
    }

    private async Task<bool> IsBelow(long candidate, long unitId, CancellationToken ct)
    {
        var parents = await db.BusinessUnits.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.ParentBusinessUnitId, ct);
        for (long? current = candidate; current is { } c; current = parents.GetValueOrDefault(c))
            if (c == unitId) return true;
        return false;
    }
}
