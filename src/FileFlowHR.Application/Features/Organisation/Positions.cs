using FluentValidation;
using FileFlowHR.Application.Abstractions;
using FileFlowHR.Application.Common;
using FileFlowHR.Application.Common.Messaging;
using FileFlowHR.Application.Common.Security;
using FileFlowHR.Domain.Organisation;
using FileFlowHR.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace FileFlowHR.Application.Features.Organisation;

public sealed record PositionRow(long Id, string Code, string Title, long BusinessUnitId, string BusinessUnit, string? Location,
    long? HolderPersonId, string? HolderName, long? ManagerPositionId, string? ManagerTitle,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo)
{
    public bool IsVacant => HolderPersonId is null && EffectiveTo is null;
}

/// <summary>Positions as of a date, limited to what the user may see.</summary>
[RequiresPermission(Permissions.OrgView)]
public sealed record ListPositionsQuery(DateOnly? AsOf = null, string? Search = null, bool IncludeClosed = false)
    : IQuery<Result<IReadOnlyList<PositionRow>>>;

public sealed class ListPositionsHandler(IPositionReader reader, IAccessScopeResolver scope)
    : IRequestHandler<ListPositionsQuery, Result<IReadOnlyList<PositionRow>>>
{
    public async Task<Result<IReadOnlyList<PositionRow>>> Handle(ListPositionsQuery request, CancellationToken ct)
    {
        var visible = await scope.PositionIdsAsync(Permissions.OrgView, ct);
        return Result<IReadOnlyList<PositionRow>>.Success(
            await reader.ListAsync(request.AsOf, visible, request.Search, request.IncludeClosed, ct));
    }
}

public sealed record PositionHolderDto(long PersonId, string Name, string AssignmentType, DateOnly From, DateOnly? To);
public sealed record ReportingLineDto(long Id, long ManagerPositionId, string ManagerTitle, string LineType, DateOnly From, DateOnly? To);
public sealed record PositionDetail(PositionRow Position, IReadOnlyList<PositionHolderDto> Holders, IReadOnlyList<ReportingLineDto> Lines,
    IReadOnlyList<PositionRow> DirectReports);

[RequiresPermission(Permissions.OrgView)]
public sealed record GetPositionQuery(long Id) : IQuery<Result<PositionDetail>>;

public sealed class GetPositionHandler(IAppDbContext db, IAccessScopeResolver scope, IPositionReader reader)
    : IRequestHandler<GetPositionQuery, Result<PositionDetail>>
{
    public async Task<Result<PositionDetail>> Handle(GetPositionQuery request, CancellationToken ct)
    {
        var visible = await scope.PositionIdsAsync(Permissions.OrgView, ct);
        if (visible is not null && !visible.Contains(request.Id)) return Result<PositionDetail>.NotFound("Position");

        var all = await reader.ListAsync(null, visible, null, includeClosed: true, ct);
        var row = all.FirstOrDefault(p => p.Id == request.Id);
        if (row is null) return Result<PositionDetail>.NotFound("Position");

        var holders = await (from a in db.PositionAssignments.AsNoTracking()
                             join person in db.People on a.PersonId equals person.Id
                             where a.PositionId == request.Id
                             orderby a.EffectiveFrom descending
                             select new PositionHolderDto(person.Id, person.FirstName + " " + person.LastName,
                                 a.AssignmentType == AssignmentType.Primary ? "Primary" : "Interim", a.EffectiveFrom, a.EffectiveTo))
            .ToListAsync(ct);
        var lines = await (from l in db.PositionReportingLines.AsNoTracking()
                           join m in db.Positions on l.ManagerPositionId equals m.Id
                           where l.PositionId == request.Id
                           orderby l.EffectiveFrom descending
                           select new ReportingLineDto(l.Id, m.Id, m.Title, l.LineType == LineType.Solid ? "Solid" : "Dotted",
                               l.EffectiveFrom, l.EffectiveTo))
            .ToListAsync(ct);

        var reports = all.Where(p => p.ManagerPositionId == request.Id && p.EffectiveTo == null).ToList();
        return new PositionDetail(row, holders, lines, reports);
    }
}

/// <summary>Reads positions with their current holder and solid-line manager (shared by several queries).</summary>
public interface IPositionReader
{
    Task<IReadOnlyList<PositionRow>> ListAsync(DateOnly? asOf, HashSet<long>? visibleIds, string? search,
        bool includeClosed, CancellationToken ct);
}

public sealed class PositionReader(IAppDbContext db, TimeProvider clock) : IPositionReader
{
    public async Task<IReadOnlyList<PositionRow>> ListAsync(DateOnly? asOf, HashSet<long>? visibleIds, string? search,
        bool includeClosed, CancellationToken ct)
    {
        var date = asOf ?? clock.Today();
        var positions = db.Positions.AsNoTracking();
        if (visibleIds is not null) positions = positions.Where(p => visibleIds.Contains(p.Id));
        if (!includeClosed)
            positions = positions.Where(p => p.EffectiveFrom <= date && (p.EffectiveTo == null || p.EffectiveTo > date));
        if (!string.IsNullOrWhiteSpace(search))
            positions = positions.Where(p => p.Title.Contains(search) || p.Code.Contains(search));

        var holders = from a in db.PositionAssignments
                      join person in db.People on a.PersonId equals person.Id
                      where a.AssignmentType == AssignmentType.Primary && a.EffectiveFrom <= date
                            && (a.EffectiveTo == null || a.EffectiveTo > date)
                      select new { a.PositionId, person.Id, Name = person.FirstName + " " + person.LastName };
        var managers = from l in db.PositionReportingLines
                       join m in db.Positions on l.ManagerPositionId equals m.Id
                       where l.LineType == LineType.Solid && l.EffectiveFrom <= date && (l.EffectiveTo == null || l.EffectiveTo > date)
                       select new { l.PositionId, ManagerId = m.Id, m.Title };

        return await (from p in positions
                      join b in db.BusinessUnits on p.BusinessUnitId equals b.Id
                      join l in db.Locations on p.LocationId equals l.Id into ls
                      from l in ls.DefaultIfEmpty()
                      join h in holders on p.Id equals h.PositionId into hs
                      from h in hs.DefaultIfEmpty()
                      join m in managers on p.Id equals m.PositionId into ms
                      from m in ms.DefaultIfEmpty()
                      orderby p.Code
                      select new PositionRow(p.Id, p.Code, p.Title, p.BusinessUnitId, b.Name, l != null ? l.Name : null,
                          h != null ? (long?)h.Id : null, h != null ? h.Name : null,
                          m != null ? (long?)m.ManagerId : null, m != null ? m.Title : null, p.EffectiveFrom, p.EffectiveTo))
            .ToListAsync(ct);
    }
}

[RequiresPermission(Permissions.OrgManage)]
public sealed record SavePositionCommand(long? Id, string Code, string Title, long BusinessUnitId, long? LocationId,
    DateOnly EffectiveFrom, long? ManagerPositionId) : ICommand<Result<long>>;

public sealed class SavePositionValidator : AbstractValidator<SavePositionCommand>
{
    public SavePositionValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.BusinessUnitId).GreaterThan(0).WithMessage("Choose a business unit.");
    }
}

public sealed class SavePositionHandler(IAppDbContext db, IReportingLineService lines) : IRequestHandler<SavePositionCommand, Result<long>>
{
    public async Task<Result<long>> Handle(SavePositionCommand request, CancellationToken ct)
    {
        if (!await db.BusinessUnits.AnyAsync(b => b.Id == request.BusinessUnitId, ct)) return Result<long>.NotFound("Business unit");
        if (request.LocationId is { } loc && !await db.Locations.AnyAsync(l => l.Id == loc, ct)) return Result<long>.NotFound("Location");

        if (request.Id is { } id)
        {
            var existing = await db.Positions.FindAsync([id], ct);
            if (existing is null) return Result<long>.NotFound("Position");
            existing.Update(request.Title, request.BusinessUnitId, request.LocationId);
            await db.SaveChangesAsync(ct);
            return existing.Id;
        }

        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Positions.AnyAsync(p => p.Code == code, ct))
            return Result<long>.Invalid($"Position code {code} is already used.");

        var position = Position.Create(code, request.Title, request.BusinessUnitId, request.LocationId, request.EffectiveFrom);
        db.Positions.Add(position);
        await db.SaveChangesAsync(ct);

        if (request.ManagerPositionId is { } managerId)
        {
            var set = await lines.SetManagerAsync(position.Id, managerId, LineType.Solid, request.EffectiveFrom, ct);
            if (!set.Succeeded) return Result<long>.Fail(set.Kind, set.Errors);
        }
        return position.Id;
    }
}

[RequiresPermission(Permissions.OrgManage)]
public sealed record ChangeManagerCommand(long PositionId, long ManagerPositionId, LineType LineType, DateOnly EffectiveFrom) : ICommand<Result>;

public sealed class ChangeManagerHandler(IReportingLineService lines) : IRequestHandler<ChangeManagerCommand, Result>
{
    public Task<Result> Handle(ChangeManagerCommand request, CancellationToken ct) =>
        lines.SetManagerAsync(request.PositionId, request.ManagerPositionId, request.LineType, request.EffectiveFrom, ct);
}

[RequiresPermission(Permissions.OrgManage)]
public sealed record EndReportingLineCommand(long ReportingLineId, DateOnly EndDate) : ICommand<Result>;

public sealed class EndReportingLineHandler(IAppDbContext db) : IRequestHandler<EndReportingLineCommand, Result>
{
    public async Task<Result> Handle(EndReportingLineCommand request, CancellationToken ct)
    {
        var line = await db.PositionReportingLines.FindAsync([request.ReportingLineId], ct);
        if (line is null) return Result.NotFound("Reporting line");
        line.EndOn(request.EndDate);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

[RequiresPermission(Permissions.OrgManage)]
public sealed record ClosePositionCommand(long PositionId, DateOnly EndDate) : ICommand<Result>;

public sealed class ClosePositionHandler(IAppDbContext db) : IRequestHandler<ClosePositionCommand, Result>
{
    public async Task<Result> Handle(ClosePositionCommand request, CancellationToken ct)
    {
        var position = await db.Positions.FindAsync([request.PositionId], ct);
        if (position is null) return Result.NotFound("Position");

        var end = request.EndDate;
        if (await db.PositionAssignments.AnyAsync(a => a.PositionId == position.Id && (a.EffectiveTo == null || a.EffectiveTo > end), ct))
            return Result.Invalid("Move the person in this position first. A position with a holder cannot be closed.");
        if (await db.PositionReportingLines.AnyAsync(l => l.ManagerPositionId == position.Id && l.LineType == LineType.Solid
                                                          && (l.EffectiveTo == null || l.EffectiveTo > end), ct))
            return Result.Invalid("Other positions still report to this one. Give them a new manager first.");

        position.EndOn(end);
        foreach (var line in await db.PositionReportingLines.Where(l => l.PositionId == position.Id && l.EffectiveTo == null).ToListAsync(ct))
            line.EndOn(end > line.EffectiveFrom ? end : line.EffectiveFrom.AddDays(1));
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Domain service for reporting lines: one open solid line per position, and no loops.</summary>
public interface IReportingLineService
{
    Task<Result> SetManagerAsync(long positionId, long managerPositionId, LineType lineType, DateOnly from, CancellationToken ct);
}

public sealed class ReportingLineService(IAppDbContext db) : IReportingLineService
{
    public async Task<Result> SetManagerAsync(long positionId, long managerPositionId, LineType lineType, DateOnly from, CancellationToken ct)
    {
        if (!await db.Positions.AnyAsync(p => p.Id == positionId, ct)) return Result.NotFound("Position");
        if (!await db.Positions.AnyAsync(p => p.Id == managerPositionId && (p.EffectiveTo == null || p.EffectiveTo > from), ct))
            return Result.NotFound("Manager position");

        var open = await db.PositionReportingLines.Where(l => l.EffectiveTo == null).ToListAsync(ct);
        if (lineType == LineType.Solid)
        {
            var solid = open.Where(l => l.LineType == LineType.Solid && l.PositionId != positionId)
                .Select(l => (l.PositionId, l.ManagerPositionId));
            if (OrgTree.WouldCreateCycle(positionId, managerPositionId, solid))
                return Result.Invalid("That would make the position report to itself through others.");

            var current = open.FirstOrDefault(l => l.PositionId == positionId && l.LineType == LineType.Solid);
            if (current is not null)
            {
                if (current.ManagerPositionId == managerPositionId) return Result.Success();
                current.EndOn(from); // the old line is kept as history
            }
        }
        else if (open.Any(l => l.PositionId == positionId && l.ManagerPositionId == managerPositionId))
        {
            return Result.Invalid("This reporting line already exists.");
        }

        db.PositionReportingLines.Add(PositionReportingLine.Create(positionId, managerPositionId, lineType, from));
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
