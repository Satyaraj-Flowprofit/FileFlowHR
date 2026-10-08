using FluentValidation;
using FileFlowHR.Application.Abstractions;
using FileFlowHR.Application.Common;
using FileFlowHR.Application.Common.Messaging;
using FileFlowHR.Application.Common.Security;
using FileFlowHR.Domain.Organisation;
using Microsoft.EntityFrameworkCore;

namespace FileFlowHR.Application.Features.Organisation;

public sealed record RegionDto(long Id, string Name, string CountryCode, string TimeZone, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

[RequiresPermission(Permissions.OrgView)]
public sealed record ListRegionsQuery : IQuery<Result<IReadOnlyList<RegionDto>>>;

public sealed class ListRegionsHandler(IAppDbContext db) : IRequestHandler<ListRegionsQuery, Result<IReadOnlyList<RegionDto>>>
{
    public async Task<Result<IReadOnlyList<RegionDto>>> Handle(ListRegionsQuery request, CancellationToken ct) =>
        await db.Regions.AsNoTracking().OrderBy(r => r.Name)
            .Select(r => new RegionDto(r.Id, r.Name, r.CountryCode, r.TimeZone, r.EffectiveFrom, r.EffectiveTo))
            .ToListAsync(ct);
}

[RequiresPermission(Permissions.OrgManage)]
public sealed record SaveRegionCommand(long? Id, string Name, string CountryCode, string TimeZone, DateOnly EffectiveFrom)
    : ICommand<Result<long>>;

public sealed class SaveRegionValidator : AbstractValidator<SaveRegionCommand>
{
    public SaveRegionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CountryCode).NotEmpty().Length(2);
        RuleFor(x => x.TimeZone).NotEmpty().MaximumLength(64);
    }
}

public sealed class SaveRegionHandler(IAppDbContext db) : IRequestHandler<SaveRegionCommand, Result<long>>
{
    public async Task<Result<long>> Handle(SaveRegionCommand request, CancellationToken ct)
    {
        Region region;
        if (request.Id is { } id)
        {
            var existing = await db.Regions.FindAsync([id], ct);
            if (existing is null) return Result<long>.NotFound("Region");
            region = existing;
            region.Update(request.Name, request.CountryCode, request.TimeZone);
        }
        else
        {
            region = Region.Create(request.Name, request.CountryCode, request.TimeZone, request.EffectiveFrom);
            db.Regions.Add(region);
        }
        await db.SaveChangesAsync(ct);
        return region.Id;
    }
}
