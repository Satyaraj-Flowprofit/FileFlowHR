using FluentValidation;
using FileFlowHR.Application.Abstractions;
using FileFlowHR.Application.Common;
using FileFlowHR.Application.Common.Messaging;
using FileFlowHR.Application.Common.Security;
using FileFlowHR.Application.Features.People;
using FileFlowHR.Domain.Companies;
using FileFlowHR.Domain.People;
using FileFlowHR.Domain.Recruitment;
using Microsoft.EntityFrameworkCore;

namespace FileFlowHR.Application.Features.Recruitment;

public sealed record JobSummary(long Id, string Title, string? Location, string BusinessUnit, DateOnly OpenDate, DateOnly? CloseDate);
public sealed record JobDetail(long Id, string Title, string Description, string? Location, string BusinessUnit,
    DateOnly OpenDate, DateOnly? CloseDate, bool AlreadyApplied);
public sealed record CareersPage(string CompanyCode, string CompanyName, IReadOnlyList<JobSummary> Jobs);

/// <summary>Reads open jobs for one company, either the public careers page or the internal job board.</summary>
/// <remarks>
/// IgnoreQueryFilters switches the company filter off for the WHOLE query in EF Core, not just one table,
/// so every table here is filtered by the explicit companyId instead.
/// </remarks>
internal static class JobQueries
{
    public static IQueryable<JobSummary> Open(IAppDbContext db, long companyId, DateOnly today, bool forStaff, long? jobPostingId = null) =>
        from j in db.JobPostings.IgnoreQueryFilters().AsNoTracking()
        join pos in db.Positions.IgnoreQueryFilters() on j.PositionId equals pos.Id
        join b in db.BusinessUnits.IgnoreQueryFilters() on pos.BusinessUnitId equals b.Id
        join l in db.Locations.IgnoreQueryFilters().Where(l => l.CompanyId == companyId) on pos.LocationId equals l.Id into ls
        from l in ls.DefaultIfEmpty()
        where (jobPostingId == null || j.Id == jobPostingId)
              && j.CompanyId == companyId && pos.CompanyId == companyId && b.CompanyId == companyId
              && j.Status == PostingStatus.Open && j.OpenDate <= today && (j.CloseDate == null || j.CloseDate > today)
              && (forStaff ? j.Visibility != PostingVisibility.External : j.Visibility != PostingVisibility.Internal)
        orderby j.OpenDate descending
        select new JobSummary(j.Id, j.Title, l != null ? l.Name : null, b.Name, j.OpenDate, j.CloseDate);
}

[AllowAnonymousRequest]
public sealed record GetCareersPageQuery(string CompanyCode) : IQuery<Result<CareersPage>>;

public sealed class GetCareersPageHandler(IAppDbContext db, TimeProvider clock) : IRequestHandler<GetCareersPageQuery, Result<CareersPage>>
{
    public async Task<Result<CareersPage>> Handle(GetCareersPageQuery request, CancellationToken ct)
    {
        var code = request.CompanyCode.Trim().ToLowerInvariant();
        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Code == code && c.Status == CompanyStatus.Active, ct);
        if (company is null) return Result<CareersPage>.NotFound("Company");

        // Public page: no user, so the company filter is applied explicitly.
        var jobs = await JobQueries.Open(db, company.Id, clock.Today(), forStaff: false).ToListAsync(ct);
        return new CareersPage(company.Code, company.LegalName, jobs);
    }
}

[AllowAnonymousRequest]
public sealed record GetPublicJobQuery(string CompanyCode, long JobPostingId) : IQuery<Result<JobDetail>>;

public sealed class GetPublicJobHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock) : IRequestHandler<GetPublicJobQuery, Result<JobDetail>>
{
    public async Task<Result<JobDetail>> Handle(GetPublicJobQuery request, CancellationToken ct)
    {
        var code = request.CompanyCode.Trim().ToLowerInvariant();
        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Code == code && c.Status == CompanyStatus.Active, ct);
        if (company is null) return Result<JobDetail>.NotFound("Company");

        var summary = await JobQueries.Open(db, company.Id, clock.Today(), forStaff: false, request.JobPostingId).FirstOrDefaultAsync(ct);
        if (summary is null) return Result<JobDetail>.NotFound("Job");

        var description = await db.JobPostings.IgnoreQueryFilters().AsNoTracking()
            .Where(j => j.CompanyId == company.Id && j.Id == request.JobPostingId).Select(j => j.Description).FirstAsync(ct);
        var applied = user.CompanyId == company.Id && user.PersonId is { } pid
                      && await db.JobApplications.IgnoreQueryFilters().AnyAsync(a => a.CompanyId == company.Id
                          && a.JobPostingId == request.JobPostingId && a.PersonId == pid, ct);
        return new JobDetail(summary.Id, summary.Title, description, summary.Location, summary.BusinessUnit,
            summary.OpenDate, summary.CloseDate, applied);
    }
}

/// <summary>Internal job board: open jobs visible to staff in the user's own company.</summary>
[RequiresPermission(Permissions.JobsBrowse)]
public sealed record ListInternalJobsQuery : IQuery<Result<IReadOnlyList<JobDetail>>>;

public sealed class ListInternalJobsHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock)
    : IRequestHandler<ListInternalJobsQuery, Result<IReadOnlyList<JobDetail>>>
{
    public async Task<Result<IReadOnlyList<JobDetail>>> Handle(ListInternalJobsQuery request, CancellationToken ct)
    {
        if (user.CompanyId is not { } companyId) return Result<IReadOnlyList<JobDetail>>.Success([]);
        var jobs = await JobQueries.Open(db, companyId, clock.Today(), forStaff: true).ToListAsync(ct);
        var ids = jobs.Select(j => j.Id).ToList();
        var descriptions = await db.JobPostings.AsNoTracking().Where(j => ids.Contains(j.Id)).ToDictionaryAsync(j => j.Id, j => j.Description, ct);
        var applied = await db.JobApplications.AsNoTracking().Where(a => a.PersonId == user.PersonId).Select(a => a.JobPostingId).ToListAsync(ct);
        return jobs.Select(j => new JobDetail(j.Id, j.Title, descriptions[j.Id], j.Location, j.BusinessUnit, j.OpenDate, j.CloseDate,
            applied.Contains(j.Id))).ToList();
    }
}

/// <summary>Apply for a job as the signed-in user. Staff apply internally; applicants externally.</summary>
[RequiresPermission(Permissions.OwnApplications)]
public sealed record ApplyForJobCommand(long JobPostingId, string? ResumePath = null) : ICommand<Result<long>>;

public sealed class ApplyForJobHandler(IAppDbContext db, ICurrentUser user, TimeProvider clock) : IRequestHandler<ApplyForJobCommand, Result<long>>
{
    public async Task<Result<long>> Handle(ApplyForJobCommand request, CancellationToken ct)
    {
        if (user.PersonId is not { } personId) return Result<long>.Invalid("Your login is not linked to a person yet.");
        var posting = await db.JobPostings.FirstOrDefaultAsync(j => j.Id == request.JobPostingId, ct);
        if (posting is null) return Result<long>.NotFound("Job");
        if (await db.JobApplications.AnyAsync(a => a.JobPostingId == posting.Id && a.PersonId == personId, ct))
            return Result<long>.Invalid("You have already applied for this job.");

        var isStaff = await db.Employments.AnyAsync(e => e.PersonId == personId && e.TerminationDate == null, ct);
        if (isStaff && await db.PositionAssignments.AnyAsync(a => a.PersonId == personId && a.PositionId == posting.PositionId && a.EffectiveTo == null, ct))
            return Result<long>.Invalid("You already hold this position.");

        var application = JobApplication.Submit(posting, personId,
            isStaff ? ApplicationSource.Internal : ApplicationSource.External, request.ResumePath, clock.UtcNow());
        db.JobApplications.Add(application);
        await db.SaveChangesAsync(ct);
        return application.Id;
    }
}

public sealed record MyApplicationDto(long Id, string JobTitle, ApplicationStatus Status, DateTime AppliedAt, bool CanWithdraw);

[RequiresPermission(Permissions.OwnApplications)]
public sealed record ListMyApplicationsQuery : IQuery<Result<IReadOnlyList<MyApplicationDto>>>;

public sealed class ListMyApplicationsHandler(IAppDbContext db, ICurrentUser user) : IRequestHandler<ListMyApplicationsQuery, Result<IReadOnlyList<MyApplicationDto>>>
{
    public async Task<Result<IReadOnlyList<MyApplicationDto>>> Handle(ListMyApplicationsQuery request, CancellationToken ct)
    {
        var rows = await (from a in db.JobApplications.AsNoTracking()
                          join j in db.JobPostings on a.JobPostingId equals j.Id
                          where a.PersonId == user.PersonId
                          orderby a.AppliedAt descending
                          select new { a.Id, j.Title, a.Status, a.AppliedAt }).ToListAsync(ct);
        return rows.Select(r => new MyApplicationDto(r.Id, r.Title, r.Status, r.AppliedAt,
            JobApplication.NextSteps(r.Status).Contains(ApplicationStatus.Withdrawn))).ToList();
    }
}

[RequiresPermission(Permissions.OwnApplications)]
public sealed record WithdrawApplicationCommand(long JobApplicationId) : ICommand<Result>;

public sealed class WithdrawApplicationHandler(IAppDbContext db, ICurrentUser user) : IRequestHandler<WithdrawApplicationCommand, Result>
{
    public async Task<Result> Handle(WithdrawApplicationCommand request, CancellationToken ct)
    {
        var application = await db.JobApplications.FirstOrDefaultAsync(a => a.Id == request.JobApplicationId && a.PersonId == user.PersonId, ct);
        if (application is null) return Result.NotFound("Application");
        application.MoveTo(ApplicationStatus.Withdrawn);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>An outside applicant creates an account on a company's careers page.</summary>
[AllowAnonymousRequest]
public sealed record RegisterApplicantCommand(string CompanyCode, string FirstName, string LastName, string Email, string Password)
    : ICommand<Result<long>>;

public sealed class RegisterApplicantValidator : AbstractValidator<RegisterApplicantCommand>
{
    public RegisterApplicantValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(10).WithMessage("Use at least 10 characters for your password.");
    }
}

public sealed class RegisterApplicantHandler(IAppDbContext db, UserContext context, IPersonNumberGenerator numbers,
    IUserAccountService accounts) : IRequestHandler<RegisterApplicantCommand, Result<long>>
{
    public async Task<Result<long>> Handle(RegisterApplicantCommand request, CancellationToken ct)
    {
        if (context.IsAuthenticated) return Result<long>.Invalid("Sign out first to create a new applicant account.");

        var code = request.CompanyCode.Trim().ToLowerInvariant();
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Code == code && c.Status == CompanyStatus.Active, ct);
        if (company is null) return Result<long>.NotFound("Company");
        context.UseCompanyForAnonymous(company.Id);

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.People.AnyAsync(p => p.Email == email, ct))
            return Result<long>.Invalid("An account with this email already exists here. Sign in instead, or reset your password.");

        var passwordErrors = await accounts.ValidatePasswordAsync(request.Password);
        if (passwordErrors.Count > 0) return Result<long>.Invalid([.. passwordErrors]);

        var person = Person.Create(await numbers.NextAsync(ct), request.FirstName, request.LastName, email, null);
        db.People.Add(person);
        await db.SaveChangesAsync(ct);

        var created = await accounts.CreateApplicantAsync(company.Id, company.Code, person.Id, email, request.Password, ct);
        return created;
    }
}
