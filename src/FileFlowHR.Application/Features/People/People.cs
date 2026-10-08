using FluentValidation;
using FileFlowHR.Application.Abstractions;
using FileFlowHR.Application.Common;
using FileFlowHR.Application.Common.Messaging;
using FileFlowHR.Application.Common.Security;
using FileFlowHR.Domain.People;
using FileFlowHR.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace FileFlowHR.Application.Features.People;

public enum PeopleFilter { Everyone, Employees, Applicants, Former }

public sealed record PersonRow(long Id, string PersonNumber, string Name, string? Email, string? PositionTitle,
    bool IsEmployee, bool IsApplicant, bool IsFormer);

[RequiresPermission(Permissions.PeopleView)]
public sealed record ListPeopleQuery(string? Search = null, PeopleFilter Filter = PeopleFilter.Everyone)
    : IQuery<Result<IReadOnlyList<PersonRow>>>;

public sealed class ListPeopleHandler(IAppDbContext db, IAccessScopeResolver scope, TimeProvider clock)
    : IRequestHandler<ListPeopleQuery, Result<IReadOnlyList<PersonRow>>>
{
    public async Task<Result<IReadOnlyList<PersonRow>>> Handle(ListPeopleQuery request, CancellationToken ct)
    {
        var today = clock.Today();
        var visible = await scope.PersonIdsAsync(Permissions.PeopleView, ct);

        var people = db.People.AsNoTracking();
        if (visible is not null) people = people.Where(p => visible.Contains(p.Id));
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            people = people.Where(p => p.FirstName.Contains(s) || p.LastName.Contains(s) || p.PersonNumber.Contains(s)
                                       || (p.Email != null && p.Email.Contains(s)));
        }

        var rows = await people.OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .Select(p => new
            {
                p.Id, p.PersonNumber, Name = p.FirstName + " " + p.LastName, p.Email,
                Position = (from a in db.PositionAssignments
                            join pos in db.Positions on a.PositionId equals pos.Id
                            where a.PersonId == p.Id && a.AssignmentType == AssignmentType.Primary
                                  && a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo > today)
                            select pos.Title).FirstOrDefault(),
                IsEmployee = db.Employments.Any(e => e.PersonId == p.Id && e.HireDate <= today
                                                     && (e.TerminationDate == null || e.TerminationDate > today)),
                WasEmployee = db.Employments.Any(e => e.PersonId == p.Id),
                IsApplicant = db.JobApplications.Any(a => a.PersonId == p.Id),
            })
            .ToListAsync(ct);

        IEnumerable<PersonRow> result = rows.Select(r => new PersonRow(r.Id, r.PersonNumber, r.Name, r.Email, r.Position,
            r.IsEmployee, r.IsApplicant, r.WasEmployee && !r.IsEmployee));
        result = request.Filter switch
        {
            PeopleFilter.Employees => result.Where(r => r.IsEmployee),
            PeopleFilter.Applicants => result.Where(r => r.IsApplicant && !r.IsEmployee),
            PeopleFilter.Former => result.Where(r => r.IsFormer),
            _ => result,
        };
        return result.ToList();
    }
}

public sealed record EmploymentDto(long Id, DateOnly HireDate, DateOnly? TerminationDate, string EmploymentType);
public sealed record AssignmentDto(long Id, long PositionId, string PositionCode, string PositionTitle, string AssignmentType, DateOnly From, DateOnly? To);
public sealed record PersonApplicationDto(long Id, long JobPostingId, string PostingTitle, string Source, string Status, DateTime AppliedAt);
public sealed record PersonRoleDto(long Id, string Role, string Scope, DateOnly From, DateOnly? To);
public sealed record PersonDetail(long Id, string PersonNumber, string FirstName, string LastName, string? Email, string? Phone,
    IReadOnlyList<EmploymentDto> Employments, IReadOnlyList<AssignmentDto> Assignments,
    IReadOnlyList<PersonApplicationDto> Applications, IReadOnlyList<PersonRoleDto> Roles, UserAccountDto? Login,
    bool IsEmployee, bool CanManage);

[RequiresPermission(Permissions.PeopleView)]
public sealed record GetPersonQuery(long Id) : IQuery<Result<PersonDetail>>;

public sealed class GetPersonHandler(IAppDbContext db, IAccessScopeResolver scope, IPermissionService permissions,
    IUserAccountService accounts, TimeProvider clock) : IRequestHandler<GetPersonQuery, Result<PersonDetail>>
{
    public async Task<Result<PersonDetail>> Handle(GetPersonQuery request, CancellationToken ct)
    {
        var visible = await scope.PersonIdsAsync(Permissions.PeopleView, ct);
        if (visible is not null && !visible.Contains(request.Id)) return Result<PersonDetail>.NotFound("Person");

        var p = await db.People.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (p is null) return Result<PersonDetail>.NotFound("Person");

        var today = clock.Today();
        var employments = await db.Employments.AsNoTracking().Where(e => e.PersonId == p.Id).OrderByDescending(e => e.HireDate)
            .Select(e => new EmploymentDto(e.Id, e.HireDate, e.TerminationDate, e.EmploymentType.ToString())).ToListAsync(ct);
        var assignments = await (from a in db.PositionAssignments.AsNoTracking()
                                 join pos in db.Positions on a.PositionId equals pos.Id
                                 where a.PersonId == p.Id
                                 orderby a.EffectiveFrom descending
                                 select new AssignmentDto(a.Id, pos.Id, pos.Code, pos.Title, a.AssignmentType.ToString(), a.EffectiveFrom, a.EffectiveTo))
            .ToListAsync(ct);
        var applications = await (from a in db.JobApplications.AsNoTracking()
                                  join j in db.JobPostings on a.JobPostingId equals j.Id
                                  where a.PersonId == p.Id
                                  orderby a.AppliedAt descending
                                  select new PersonApplicationDto(a.Id, j.Id, j.Title, a.Source.ToString(), a.Status.ToString(), a.AppliedAt))
            .ToListAsync(ct);
        var grants = await db.RoleGrants.AsNoTracking().Where(g => g.PersonId == p.Id).OrderByDescending(g => g.EffectiveFrom).ToListAsync(ct);
        var roles = grants.Select(g => new PersonRoleDto(g.Id, Roles.DisplayName(RoleAssignment.RoleCode(g.RoleCode)),
            g.ScopeType == ScopeType.Company ? "Whole company" : $"{g.ScopeType} {g.ScopeId}", g.EffectiveFrom, g.EffectiveTo)).ToList();

        var manageable = await scope.PersonIdsAsync(Permissions.PeopleManage, ct);
        var canManage = permissions.Has(Permissions.PeopleManage) && (manageable is null || manageable.Contains(p.Id));

        return new PersonDetail(p.Id, p.PersonNumber, p.FirstName, p.LastName, p.Email, p.Phone, employments, assignments,
            applications, roles, await accounts.FindForPersonAsync(p.Id, ct),
            employments.Any(e => e.HireDate <= today && (e.TerminationDate == null || e.TerminationDate > today)), canManage);
    }
}

[RequiresPermission(Permissions.PeopleManage)]
public sealed record SavePersonCommand(long? Id, string FirstName, string LastName, string? Email, string? Phone) : ICommand<Result<long>>;

public sealed class SavePersonValidator : AbstractValidator<SavePersonCommand>
{
    public SavePersonValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(30);
    }
}

public sealed class SavePersonHandler(IAppDbContext db, IPersonNumberGenerator numbers) : IRequestHandler<SavePersonCommand, Result<long>>
{
    public async Task<Result<long>> Handle(SavePersonCommand request, CancellationToken ct)
    {
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        if (email is not null && await db.People.AnyAsync(p => p.Email == email && p.Id != (request.Id ?? 0), ct))
            return Result<long>.Invalid("Another person in this company already uses that email.");

        if (request.Id is { } id)
        {
            var existing = await db.People.FindAsync([id], ct);
            if (existing is null) return Result<long>.NotFound("Person");
            existing.Update(request.FirstName, request.LastName, email, request.Phone);
            await db.SaveChangesAsync(ct);
            return existing.Id;
        }

        var person = Person.Create(await numbers.NextAsync(ct), request.FirstName, request.LastName, email, request.Phone);
        db.People.Add(person);
        await db.SaveChangesAsync(ct);
        return person.Id;
    }
}
