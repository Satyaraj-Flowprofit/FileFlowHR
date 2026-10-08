using FileFlowHR.Domain.Companies;
using FileFlowHR.Domain.Organisation;
using FileFlowHR.Domain.People;
using FileFlowHR.Domain.Recruitment;
using FileFlowHR.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace FileFlowHR.Application.Abstractions;

/// <summary>
/// The database as the use cases see it (unit of work). Every company-owned set is already
/// filtered to the current company; use IgnoreQueryFilters only with an explicit CompanyId.
/// </summary>
public interface IAppDbContext
{
    DbSet<Company> Companies { get; }
    DbSet<CompanyLoginProvider> CompanyLoginProviders { get; }
    DbSet<Region> Regions { get; }
    DbSet<BusinessUnit> BusinessUnits { get; }
    DbSet<Location> Locations { get; }
    DbSet<Position> Positions { get; }
    DbSet<PositionReportingLine> PositionReportingLines { get; }
    DbSet<Person> People { get; }
    DbSet<Employment> Employments { get; }
    DbSet<PositionAssignment> PositionAssignments { get; }
    DbSet<JobPosting> JobPostings { get; }
    DbSet<JobApplication> JobApplications { get; }
    DbSet<RoleGrant> RoleGrants { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
