using FileFlowHR.Application.Abstractions;
using FileFlowHR.Application.Common;
using FileFlowHR.Application.Common.Messaging;
using FileFlowHR.Application.Common.Security;
using FileFlowHR.Domain.People;
using Microsoft.EntityFrameworkCore;

namespace FileFlowHR.Application.Features.People;

/// <summary>Rules shared by hiring and moving people, so every path checks them the same way.</summary>
public interface IStaffingService
{
    Task<Result> HireAsync(long personId, long positionId, DateOnly hireDate, EmploymentType type, CancellationToken ct);
    Task<Result> AssignAsync(long personId, long positionId, AssignmentType type, DateOnly from, DateOnly? to, CancellationToken ct);
}

public sealed class StaffingService(IAppDbContext db, IUserAccountService accounts) : IStaffingService
{
    public async Task<Result> HireAsync(long personId, long positionId, DateOnly hireDate, EmploymentType type, CancellationToken ct)
    {
        if (!await db.People.AnyAsync(p => p.Id == personId, ct)) return Result.NotFound("Person");
        if (await db.Employments.AnyAsync(e => e.PersonId == personId && (e.TerminationDate == null || e.TerminationDate > hireDate), ct))
            return Result.Invalid("This person is already employed on that date.");

        var vacancy = await CheckVacantAsync(positionId, AssignmentType.Primary, hireDate, null, ct);
        if (!vacancy.Succeeded) return vacancy;

        db.Employments.Add(Employment.Hire(personId, hireDate, type));
        db.PositionAssignments.Add(PositionAssignment.Create(personId, positionId, AssignmentType.Primary, hireDate, null));
        await db.SaveChangesAsync(ct);
        await accounts.MarkInternalAsync(personId, ct);
        return Result.Success();
    }

    public async Task<Result> AssignAsync(long personId, long positionId, AssignmentType type, DateOnly from, DateOnly? to, CancellationToken ct)
    {
        if (!await db.Employments.AnyAsync(e => e.PersonId == personId && e.HireDate <= from
                                                && (e.TerminationDate == null || e.TerminationDate > from), ct))
            return Result.Invalid("Only someone employed on that date can be placed in a position.");

        var vacancy = await CheckVacantAsync(positionId, type, from, to, ct);
        if (!vacancy.Succeeded) return vacancy;

        if (type == AssignmentType.Primary)
        {
            // A person has one primary position at a time: the old one ends the day the new one starts.
            var current = await db.PositionAssignments.FirstOrDefaultAsync(a => a.PersonId == personId
                && a.AssignmentType == AssignmentType.Primary && a.EffectiveTo == null, ct);
            if (current is not null)
            {
                if (current.PositionId == positionId) return Result.Invalid("The person already holds this position.");
                current.EndOn(from);
            }
        }

        db.PositionAssignments.Add(PositionAssignment.Create(personId, positionId, type, from, to));
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Result> CheckVacantAsync(long positionId, AssignmentType type, DateOnly from, DateOnly? to, CancellationToken ct)
    {
        var position = await db.Positions.FindAsync([positionId], ct);
        if (position is null) return Result.NotFound("Position");
        if (!position.IsActiveOn(from)) return Result.Invalid("The position is not open on that date.");

        var end = to ?? DateOnly.MaxValue;
        var overlapping = await db.PositionAssignments.AnyAsync(a => a.PositionId == positionId && a.AssignmentType == type
            && a.EffectiveFrom < end && (a.EffectiveTo == null || a.EffectiveTo > from), ct);
        return overlapping
            ? Result.Invalid(type == AssignmentType.Primary
                ? "Someone already holds this position on that date."
                : "This position already has interim cover for part of that period.")
            : Result.Success();
    }
}

[RequiresPermission(Permissions.PeopleManage)]
public sealed record HirePersonCommand(long PersonId, long PositionId, DateOnly HireDate, EmploymentType EmploymentType) : ICommand<Result>;

public sealed class HirePersonHandler(IStaffingService staffing) : IRequestHandler<HirePersonCommand, Result>
{
    public Task<Result> Handle(HirePersonCommand r, CancellationToken ct) => staffing.HireAsync(r.PersonId, r.PositionId, r.HireDate, r.EmploymentType, ct);
}

[RequiresPermission(Permissions.PeopleManage)]
public sealed record AssignPositionCommand(long PersonId, long PositionId, AssignmentType AssignmentType, DateOnly From, DateOnly? To) : ICommand<Result>;

public sealed class AssignPositionHandler(IStaffingService staffing) : IRequestHandler<AssignPositionCommand, Result>
{
    public Task<Result> Handle(AssignPositionCommand r, CancellationToken ct) => staffing.AssignAsync(r.PersonId, r.PositionId, r.AssignmentType, r.From, r.To, ct);
}

/// <summary>Ends employment, frees the person's positions, ends their roles and switches off their login.</summary>
[RequiresPermission(Permissions.PeopleManage)]
public sealed record TerminateEmploymentCommand(long PersonId, DateOnly LeavingDate) : ICommand<Result>;

public sealed class TerminateEmploymentHandler(IAppDbContext db, IUserAccountService accounts, ICurrentUser user)
    : IRequestHandler<TerminateEmploymentCommand, Result>
{
    public async Task<Result> Handle(TerminateEmploymentCommand request, CancellationToken ct)
    {
        if (request.PersonId == user.PersonId) return Result.Invalid("You cannot end your own employment.");

        var employment = await db.Employments.FirstOrDefaultAsync(e => e.PersonId == request.PersonId && e.TerminationDate == null, ct);
        if (employment is null) return Result.Invalid("This person has no current employment.");

        var date = request.LeavingDate;
        employment.Terminate(date);

        foreach (var a in await db.PositionAssignments.Where(a => a.PersonId == request.PersonId && (a.EffectiveTo == null || a.EffectiveTo > date)).ToListAsync(ct))
        {
            if (a.EffectiveFrom >= date) return Result.Invalid("The person starts a position on or after that date. Change it first.");
            if (a.EffectiveTo is null) a.EndOn(date);
        }
        foreach (var g in await db.RoleGrants.Where(g => g.PersonId == request.PersonId && g.EffectiveTo == null).ToListAsync(ct))
            if (g.EffectiveFrom < date) g.EndOn(date);

        await db.SaveChangesAsync(ct);
        await accounts.DisableForPersonAsync(request.PersonId, ct);
        return Result.Success();
    }
}
