using FluentValidation;
using FileFlowHR.Application.Abstractions;
using FileFlowHR.Application.Common;
using FileFlowHR.Application.Common.Messaging;
using FileFlowHR.Application.Common.Security;
using FileFlowHR.Domain.Organisation;
using Microsoft.EntityFrameworkCore;

namespace FileFlowHR.Application.Features.Organisation;

public sealed record LocationDto(long Id, long RegionId, string RegionName, string Name, string? City, string CountryCode);

[RequiresPermission(Permissions.OrgView)]
public sealed record ListLocationsQuery : IQuery<Result<IReadOnlyList<LocationDto>>>;

public sealed class ListLocationsHandler(IAppDbContext db) : IRequestHandler<ListLocationsQuery, Result<IReadOnlyList<LocationDto>>>
{
    public async Task<Result<IReadOnlyList<LocationDto>>> Handle(ListLocationsQuery request, CancellationToken ct) =>
        await (from l in db.Locations.AsNoTracking()
               join r in db.Regions on l.RegionId equals r.Id
               orderby l.Name
               select new LocationDto(l.Id, l.RegionId, r.Name, l.Name, l.City, l.CountryCode))
            .ToListAsync(ct);
}

[RequiresPermission(Permissions.OrgManage)]
public sealed record SaveLocationCommand(long? Id, long RegionId, string Name, string? City, string CountryCode) : ICommand<Result<long>>;

public sealed class SaveLocationValidator : AbstractValidator<SaveLocationCommand>
{
    public SaveLocationValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CountryCode).NotEmpty().Length(2);
        RuleFor(x => x.RegionId).GreaterThan(0).WithMessage("Choose a region.");
    }
}

public sealed class SaveLocationHandler(IAppDbContext db) : IRequestHandler<SaveLocationCommand, Result<long>>
{
    public async Task<Result<long>> Handle(SaveLocationCommand request, CancellationToken ct)
    {
        if (!await db.Regions.AnyAsync(r => r.Id == request.RegionId, ct)) return Result<long>.NotFound("Region");

        Location location;
        if (request.Id is { } id)
        {
            var existing = await db.Locations.FindAsync([id], ct);
            if (existing is null) return Result<long>.NotFound("Location");
            location = existing;
            location.Update(request.Name, request.City, request.CountryCode);
        }
        else
        {
            location = Location.Create(request.RegionId, request.Name, request.City, request.CountryCode);
            db.Locations.Add(location);
        }
        await db.SaveChangesAsync(ct);
        return location.Id;
    }
}
