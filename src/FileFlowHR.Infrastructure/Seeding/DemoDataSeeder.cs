using FileFlowHR.Application.Common.Security;
using FileFlowHR.Domain.Companies;
using FileFlowHR.Domain.Organisation;
using FileFlowHR.Domain.People;
using FileFlowHR.Domain.Recruitment;
using FileFlowHR.Domain.Security;
using FileFlowHR.Infrastructure.Identity;
using FileFlowHR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FileFlowHR.Infrastructure.Seeding;

/// <summary>
/// Loads the two sample companies from the kickoff schema document (Acme and Globex) into an
/// empty database, so the team can sign in and click around straight away.
/// </summary>
public sealed class DemoDataSeeder(FileFlowHRDbContext db, UserContext context, UserManager<UserAccount> users, ILogger<DemoDataSeeder> logger)
{
    public const string DemoPassword = "FileFlowHR#2026";

    private static DateOnly D(int y, int m, int d) => new(y, m, d);
    private static DateTime T(int y, int m, int d, int h, int min) => new(y, m, d, h, min, 0, DateTimeKind.Utc);

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Companies.AnyAsync(ct)) return;
        logger.LogInformation("Empty database: loading Acme and Globex demo data.");

        var acme = Company.Create("acme", "Acme Manufacturing Inc.", "US");
        var globex = Company.Create("globex", "Globex Technologies Pvt Ltd", "IN");
        db.Companies.AddRange(acme, globex);
        await db.SaveChangesAsync(ct);

        await SeedAcmeAsync(acme, ct);
        await SeedGlobexAsync(globex, ct);
    }

    private async Task SeedAcmeAsync(Company acme, CancellationToken ct)
    {
        context.UseCompanyForAnonymous(acme.Id);
        // Acme has no login system: staff use FileFlowHR passwords. Turn on "Require MFA" in Settings to try one-time codes.
        db.CompanyLoginProviders.Add(CompanyLoginProvider.Password(requireMfa: false));

        var usEast = Region.Create("US East", "US", "America/New_York", D(2020, 1, 1));
        var india = Region.Create("India", "IN", "Asia/Kolkata", D(2024, 4, 1));
        db.Regions.AddRange(usEast, india);
        await db.SaveChangesAsync(ct);

        var corporate = BusinessUnit.Create(usEast.Id, null, "Corporate", D(2020, 1, 1));
        var operations = BusinessUnit.Create(usEast.Id, null, "Operations", D(2020, 1, 1));
        var engineering = BusinessUnit.Create(india.Id, null, "Engineering", D(2024, 4, 1));
        db.BusinessUnits.AddRange(corporate, operations, engineering);
        await db.SaveChangesAsync(ct);
        var sales = BusinessUnit.Create(usEast.Id, operations.Id, "Sales", D(2021, 6, 1));
        db.BusinessUnits.Add(sales);

        var newark = Location.Create(usEast.Id, "Newark Plant", "Newark", "US");
        var chennai = Location.Create(india.Id, "Chennai Office", "Chennai", "IN");
        db.Locations.AddRange(newark, chennai);
        await db.SaveChangesAsync(ct);

        var ceo = Position.Create("CEO-001", "Chief Executive Officer", corporate.Id, newark.Id, D(2020, 1, 1));
        var vpOps = Position.Create("VPO-001", "VP Operations", operations.Id, newark.Id, D(2020, 1, 1));
        var hrm = Position.Create("HRM-001", "HR Manager", corporate.Id, newark.Id, D(2020, 1, 1));
        var plant = Position.Create("PLS-001", "Plant Supervisor", operations.Id, newark.Id, D(2020, 1, 1));
        var salesMgr = Position.Create("SLM-001", "Sales Manager", sales.Id, newark.Id, D(2021, 6, 1));
        var engLead = Position.Create("ENL-001", "Engineering Lead", engineering.Id, chennai.Id, D(2024, 4, 1));
        var swe = Position.Create("SWE-001", "Software Engineer", engineering.Id, chennai.Id, D(2024, 4, 1));
        var salesExec = Position.Create("SLX-001", "Sales Executive", sales.Id, newark.Id, D(2026, 9, 1));
        var quality = Position.Create("QAN-001", "Quality Analyst", operations.Id, newark.Id, D(2026, 9, 1));
        db.Positions.AddRange(ceo, vpOps, hrm, plant, salesMgr, engLead, swe, salesExec, quality);
        await db.SaveChangesAsync(ct);

        var plantOld = PositionReportingLine.Create(plant.Id, ceo.Id, LineType.Solid, D(2020, 1, 1));
        plantOld.EndOn(D(2026, 3, 1));
        db.PositionReportingLines.AddRange(
            PositionReportingLine.Create(vpOps.Id, ceo.Id, LineType.Solid, D(2020, 1, 1)),
            PositionReportingLine.Create(hrm.Id, ceo.Id, LineType.Solid, D(2020, 1, 1)),
            plantOld,
            PositionReportingLine.Create(plant.Id, vpOps.Id, LineType.Solid, D(2026, 3, 1)),
            PositionReportingLine.Create(salesMgr.Id, vpOps.Id, LineType.Solid, D(2021, 6, 1)),
            PositionReportingLine.Create(engLead.Id, ceo.Id, LineType.Solid, D(2024, 4, 1)),
            PositionReportingLine.Create(engLead.Id, vpOps.Id, LineType.Dotted, D(2024, 4, 1)),
            PositionReportingLine.Create(swe.Id, engLead.Id, LineType.Solid, D(2024, 4, 1)),
            PositionReportingLine.Create(salesExec.Id, salesMgr.Id, LineType.Solid, D(2026, 9, 1)),
            PositionReportingLine.Create(quality.Id, plant.Id, LineType.Solid, D(2026, 9, 1)));

        var maria = Person.Create("P0001", "Maria", "Lopez", "maria.lopez@acme.com", "+1 973 555 0101");
        var david = Person.Create("P0002", "David", "Chen", "david.chen@acme.com", "+1 973 555 0102");
        var priya = Person.Create("P0003", "Priya", "Nair", "priya.nair@acme.com", "+1 973 555 0103");
        var james = Person.Create("P0004", "James", "Carter", "james.carter@acme.com", null);
        var sara = Person.Create("P0005", "Sara", "Kim", "sara.kim@acme.com", null);
        var arjun = Person.Create("P0006", "Arjun", "Mehta", "arjun.mehta@acme.com", null);
        var ravi = Person.Create("P0007", "Ravi", "Kumar", "ravi.kumar@gmail.com", "+91 98400 00107");
        var tom = Person.Create("P0008", "Tom", "Baker", "tom.baker@outlook.com", null);
        var linda = Person.Create("P0009", "Linda", "Park", "linda.park@gmail.com", null);
        db.People.AddRange(maria, david, priya, james, sara, arjun, ravi, tom, linda);
        await db.SaveChangesAsync(ct);

        var lindaJob = Employment.Hire(linda.Id, D(2020, 1, 1), EmploymentType.FullTime);
        lindaJob.Terminate(D(2026, 3, 1));
        db.Employments.AddRange(
            Employment.Hire(maria.Id, D(2020, 1, 1), EmploymentType.FullTime),
            Employment.Hire(david.Id, D(2020, 1, 1), EmploymentType.FullTime),
            Employment.Hire(priya.Id, D(2020, 1, 1), EmploymentType.FullTime),
            lindaJob,
            Employment.Hire(james.Id, D(2026, 3, 1), EmploymentType.FullTime),
            Employment.Hire(sara.Id, D(2021, 6, 1), EmploymentType.FullTime),
            Employment.Hire(arjun.Id, D(2024, 4, 1), EmploymentType.FullTime),
            Employment.Hire(ravi.Id, D(2026, 10, 1), EmploymentType.FullTime));

        var lindaSeat = PositionAssignment.Create(linda.Id, plant.Id, AssignmentType.Primary, D(2020, 1, 1), null);
        lindaSeat.EndOn(D(2026, 3, 1));
        db.PositionAssignments.AddRange(
            PositionAssignment.Create(maria.Id, ceo.Id, AssignmentType.Primary, D(2020, 1, 1), null),
            PositionAssignment.Create(david.Id, vpOps.Id, AssignmentType.Primary, D(2020, 1, 1), null),
            PositionAssignment.Create(priya.Id, hrm.Id, AssignmentType.Primary, D(2020, 1, 1), null),
            lindaSeat,
            PositionAssignment.Create(james.Id, plant.Id, AssignmentType.Primary, D(2026, 3, 1), null),
            PositionAssignment.Create(sara.Id, salesMgr.Id, AssignmentType.Primary, D(2021, 6, 1), null),
            PositionAssignment.Create(david.Id, salesMgr.Id, AssignmentType.Interim, D(2026, 6, 1), D(2026, 7, 1)),
            PositionAssignment.Create(arjun.Id, engLead.Id, AssignmentType.Primary, D(2024, 4, 1), null),
            PositionAssignment.Create(ravi.Id, swe.Id, AssignmentType.Primary, D(2026, 10, 1), null));
        await db.SaveChangesAsync(ct);

        var sweJob = JobPosting.Draft(swe.Id, "Software Engineer, Chennai",
            "Build and support Acme's plant systems. 3+ years of C# and SQL.", PostingVisibility.Both, D(2026, 8, 1), D(2026, 9, 15));
        var salesJob = JobPosting.Draft(salesExec.Id, "Sales Executive, Newark",
            "Grow Acme's customer base in the North East. Travel about 30%.", PostingVisibility.External, D(2026, 9, 20), null);
        var qualityJob = JobPosting.Draft(quality.Id, "Quality Analyst, Newark Plant",
            "Own quality checks on the Newark production lines. Staff and outside candidates welcome.", PostingVisibility.Both, D(2026, 9, 25), null);
        foreach (var job in new[] { sweJob, salesJob, qualityJob }) job.Publish();
        db.JobPostings.AddRange(sweJob, salesJob, qualityJob);
        await db.SaveChangesAsync(ct);

        var raviApp = JobApplication.Submit(sweJob, ravi.Id, ApplicationSource.External, null, T(2026, 8, 3, 10, 12));
        var tomApp = JobApplication.Submit(sweJob, tom.Id, ApplicationSource.External, null, T(2026, 8, 5, 16, 40));
        var jamesApp = JobApplication.Submit(sweJob, james.Id, ApplicationSource.Internal, null, T(2026, 8, 6, 9, 5));
        var tomSales = JobApplication.Submit(salesJob, tom.Id, ApplicationSource.External, null, T(2026, 9, 22, 11, 30));
        foreach (var s in new[] { ApplicationStatus.Screening, ApplicationStatus.Interview, ApplicationStatus.Offer, ApplicationStatus.Hired })
            raviApp.MoveTo(s);
        tomApp.MoveTo(ApplicationStatus.Screening);
        tomApp.MoveTo(ApplicationStatus.Rejected);
        jamesApp.MoveTo(ApplicationStatus.Withdrawn);
        tomSales.MoveTo(ApplicationStatus.Screening);
        sweJob.MarkFilled(D(2026, 9, 15));
        db.JobApplications.AddRange(raviApp, tomApp, jamesApp, tomSales);

        db.RoleGrants.AddRange(
            RoleGrant.Create(priya.Id, GrantableRole.ClientAdmin, ScopeType.Company, null, D(2026, 1, 5), null, null),
            RoleGrant.Create(priya.Id, GrantableRole.HrAdmin, ScopeType.Company, null, D(2026, 1, 5), null, null),
            RoleGrant.Create(sara.Id, GrantableRole.Recruiter, ScopeType.BusinessUnit, engineering.Id, D(2026, 7, 15), null, priya.Id),
            RoleGrant.Create(david.Id, GrantableRole.Auditor, ScopeType.Region, usEast.Id, D(2026, 10, 1), D(2027, 1, 1), priya.Id));
        await db.SaveChangesAsync(ct);

        await AddLogin(acme, maria, UserTypes.Internal);
        await AddLogin(acme, david, UserTypes.Internal);
        await AddLogin(acme, priya, UserTypes.Internal);
        await AddLogin(acme, james, UserTypes.Internal);
        await AddLogin(acme, sara, UserTypes.Internal);
        await AddLogin(acme, arjun, UserTypes.Internal);
        await AddLogin(acme, ravi, UserTypes.Internal);
        await AddLogin(acme, tom, UserTypes.External);
        await AddLogin(acme, linda, UserTypes.Internal, UserStatuses.Disabled);
    }

    private async Task SeedGlobexAsync(Company globex, CancellationToken ct)
    {
        context.UseCompanyForAnonymous(globex.Id);
        // Globex's staff would normally use their Microsoft login. It is switched off here because it needs
        // a real Entra ID app registration; password sign-in is on so you can try Globex locally.
        db.CompanyLoginProviders.AddRange(
            CompanyLoginProvider.Password(requireMfa: false),
            CompanyLoginProvider.Oidc("Sign in with Globex Microsoft",
                "https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/v2.0",
                "00000000-0000-0000-0000-000000000000", "globex-oidc-secret", "globex.com", isEnabled: false));

        var karnataka = Region.Create("Karnataka", "IN", "Asia/Kolkata", D(2022, 1, 1));
        db.Regions.Add(karnataka);
        await db.SaveChangesAsync(ct);
        var technology = BusinessUnit.Create(karnataka.Id, null, "Technology", D(2022, 1, 1));
        var hq = Location.Create(karnataka.Id, "Globex HQ", "Bengaluru", "IN");
        db.AddRange(technology, hq);
        await db.SaveChangesAsync(ct);

        var ceo = Position.Create("CEO-001", "Chief Executive Officer", technology.Id, hq.Id, D(2022, 1, 1));
        var dev = Position.Create("DEV-001", "Developer", technology.Id, hq.Id, D(2022, 1, 1));
        db.Positions.AddRange(ceo, dev);
        await db.SaveChangesAsync(ct);
        db.PositionReportingLines.Add(PositionReportingLine.Create(dev.Id, ceo.Id, LineType.Solid, D(2022, 1, 1)));

        var jane = Person.Create("P0001", "Jane", "Doe", "jane.doe@globex.com", null);
        var meena = Person.Create("P0002", "Meena", "Iyer", "meena.iyer@gmail.com", null);
        db.People.AddRange(jane, meena);
        await db.SaveChangesAsync(ct);

        db.Employments.Add(Employment.Hire(jane.Id, D(2022, 1, 1), EmploymentType.FullTime));
        db.PositionAssignments.Add(PositionAssignment.Create(jane.Id, ceo.Id, AssignmentType.Primary, D(2022, 1, 1), null));
        var devJob = JobPosting.Draft(dev.Id, "Developer, Bengaluru", "Join Globex's product team. .NET and Azure experience preferred.",
            PostingVisibility.Both, D(2026, 9, 25), null);
        devJob.Publish();
        db.JobPostings.Add(devJob);
        await db.SaveChangesAsync(ct);

        db.JobApplications.Add(JobApplication.Submit(devJob, meena.Id, ApplicationSource.External, null, T(2026, 10, 2, 9, 50)));
        db.RoleGrants.AddRange(
            RoleGrant.Create(jane.Id, GrantableRole.ClientAdmin, ScopeType.Company, null, D(2026, 9, 1), null, null),
            RoleGrant.Create(jane.Id, GrantableRole.HrAdmin, ScopeType.Company, null, D(2026, 9, 1), null, null));
        await db.SaveChangesAsync(ct);

        await AddLogin(globex, jane, UserTypes.Internal);
        await AddLogin(globex, meena, UserTypes.External);
    }

    private async Task AddLogin(Company company, Person person, string userType, string status = UserStatuses.Active)
    {
        var account = new UserAccount
        {
            CompanyId = company.Id,
            PersonId = person.Id,
            UserName = UserAccount.MakeUserName(company.Code, person.Email!),
            Email = person.Email,
            EmailConfirmed = true,
            UserType = userType,
            Status = status,
            LockoutEnabled = true,
        };
        var result = await users.CreateAsync(account, DemoPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
