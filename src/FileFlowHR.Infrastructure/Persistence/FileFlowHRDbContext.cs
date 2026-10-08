using System.Linq.Expressions;
using FileFlowHR.Application.Abstractions;
using FileFlowHR.Domain.Common;
using FileFlowHR.Domain.Companies;
using FileFlowHR.Domain.Organisation;
using FileFlowHR.Domain.People;
using FileFlowHR.Domain.Recruitment;
using FileFlowHR.Domain.Security;
using FileFlowHR.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FileFlowHR.Infrastructure.Persistence;

/// <summary>
/// The single kickoff database: every client company, its HR data and its sign-in accounts.
/// Company-owned rows are filtered to the current company on every query (fail closed: no company, no rows).
/// </summary>
public sealed class FileFlowHRDbContext(DbContextOptions<FileFlowHRDbContext> options, ICompanyContext companyContext)
    : IdentityUserContext<UserAccount, long, UserClaim, UserExternalLogin, UserToken>(options), IAppDbContext
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyLoginProvider> CompanyLoginProviders => Set<CompanyLoginProvider>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<BusinessUnit> BusinessUnits => Set<BusinessUnit>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<PositionReportingLine> PositionReportingLines => Set<PositionReportingLine>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Employment> Employments => Set<Employment>();
    public DbSet<PositionAssignment> PositionAssignments => Set<PositionAssignment>();
    public DbSet<JobPosting> JobPostings => Set<JobPosting>();
    public DbSet<JobApplication> JobApplications => Set<JobApplication>();
    public DbSet<RoleGrant> RoleGrants => Set<RoleGrant>();

    /// <summary>Read by the query filters. -1 matches nothing.</summary>
    public long CurrentCompanyId => companyContext.CompanyId ?? -1;

    public bool IsSqlServer => Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer";

    /// <summary>
    /// Fixed here rather than read from IdentityOptions, so the design-time tools (which build the
    /// context without the app's services) and the running app produce the same model.
    /// </summary>
    protected override Version SchemaVersion => IdentitySchemaVersions.Version2;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(FileFlowHRDbContext).Assembly);

        foreach (var entity in builder.Model.GetEntityTypes().Where(e => typeof(ICompanyOwned).IsAssignableFrom(e.ClrType)))
        {
            // Company isolation: e => e.CompanyId == this.CurrentCompanyId
            var e = Expression.Parameter(entity.ClrType, "e");
            var filter = Expression.Lambda(
                Expression.Equal(Expression.Property(e, nameof(ICompanyOwned.CompanyId)),
                    Expression.Property(Expression.Constant(this), nameof(CurrentCompanyId))), e);
            builder.Entity(entity.ClrType).HasQueryFilter(filter);
            builder.Entity(entity.ClrType).HasOne(typeof(Company)).WithMany().HasForeignKey(nameof(ICompanyOwned.CompanyId))
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity(entity.ClrType).Property<long?>(AuditColumns.UpdatedBy);

            // SQL Server keeps every earlier version of each row (system-versioned temporal tables).
            if (IsSqlServer)
            {
                var table = entity.GetTableName()!;
                builder.Entity(entity.ClrType).ToTable(table, t => t.IsTemporal(h =>
                {
                    h.UseHistoryTable(table + "History");
                    h.HasPeriodStart("ValidFrom");
                    h.HasPeriodEnd("ValidTo");
                }));
            }
        }

        // Every enum is stored as a lowercase code, as in the schema document.
        foreach (var property in builder.Model.GetEntityTypes().SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType.IsEnum))
        {
            property.SetValueConverter((Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter)
                Activator.CreateInstance(typeof(SnakeCaseEnumConverter<>).MakeGenericType(property.ClrType))!);
            property.SetMaxLength(20);
            property.SetIsUnicode(false);
        }
    }
}

public static class AuditColumns
{
    public const string UpdatedBy = "UpdatedBy";
}
