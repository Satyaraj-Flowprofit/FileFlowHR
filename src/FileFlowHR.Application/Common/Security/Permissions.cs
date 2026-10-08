namespace FileFlowHR.Application.Common.Security;

/// <summary>Every action the app controls. Fixed in code for kickoff; tables come with custom roles.</summary>
public static class Permissions
{
    public const string DashboardView = "dashboard.view";
    public const string OrgView = "org.view";
    public const string OrgManage = "org.manage";
    public const string PeopleView = "people.view";
    public const string PeopleManage = "people.manage";
    public const string UsersManage = "users.manage";
    public const string PostingsManage = "postings.manage";
    public const string ApplicationsManage = "applications.manage";
    public const string JobsBrowse = "jobs.browse";
    public const string OwnApplications = "applications.own";
    public const string RolesManage = "roles.manage";
    public const string SettingsManage = "settings.manage";

    public static readonly IReadOnlyList<string> All =
    [
        DashboardView, OrgView, OrgManage, PeopleView, PeopleManage, UsersManage, PostingsManage,
        ApplicationsManage, JobsBrowse, OwnApplications, RolesManage, SettingsManage,
    ];
}

public static class Roles
{
    public const string ClientAdmin = "client_admin";
    public const string HrAdmin = "hr_admin";
    public const string Recruiter = "recruiter";
    public const string Auditor = "auditor";
    public const string Employee = "employee";
    public const string Manager = "manager";
    public const string Applicant = "applicant";

    public static string DisplayName(string role) => role switch
    {
        ClientAdmin => "Client Admin",
        HrAdmin => "HR Admin",
        Recruiter => "Recruiter",
        Auditor => "Auditor",
        Employee => "Employee",
        Manager => "Manager",
        Applicant => "Applicant",
        _ => role,
    };
}

/// <summary>How far a permission reaches. Higher values reach further.</summary>
public enum AccessScope { Self = 0, Team = 1, BusinessUnit = 2, Region = 3, Company = 4 }

/// <summary>
/// What each role may do and the widest scope it may do it in. A granted role can be narrower
/// (for example a Recruiter for one business unit), never wider.
/// </summary>
public static class RolePermissions
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, AccessScope>> Matrix =
        new Dictionary<string, IReadOnlyDictionary<string, AccessScope>>
        {
            [Roles.ClientAdmin] = Map(AccessScope.Company, Permissions.DashboardView, Permissions.OrgView,
                Permissions.PeopleView, Permissions.UsersManage, Permissions.RolesManage, Permissions.SettingsManage),
            [Roles.HrAdmin] = Map(AccessScope.Company, Permissions.DashboardView, Permissions.OrgView, Permissions.OrgManage,
                Permissions.PeopleView, Permissions.PeopleManage, Permissions.UsersManage, Permissions.PostingsManage,
                Permissions.ApplicationsManage),
            [Roles.Recruiter] = Map(AccessScope.BusinessUnit, Permissions.DashboardView, Permissions.OrgView,
                Permissions.PeopleView, Permissions.PostingsManage, Permissions.ApplicationsManage),
            [Roles.Auditor] = Map(AccessScope.Company, Permissions.DashboardView, Permissions.OrgView, Permissions.PeopleView),
            [Roles.Manager] = Map(AccessScope.Team, Permissions.DashboardView, Permissions.PeopleView),
            [Roles.Employee] = Merge(
                Map(AccessScope.Self, Permissions.DashboardView, Permissions.PeopleView, Permissions.OwnApplications),
                Map(AccessScope.Company, Permissions.OrgView, Permissions.JobsBrowse)),
            [Roles.Applicant] = Map(AccessScope.Self, Permissions.OwnApplications),
        };

    private static Dictionary<string, AccessScope> Map(AccessScope scope, params string[] permissions) =>
        permissions.ToDictionary(p => p, _ => scope);

    private static Dictionary<string, AccessScope> Merge(params Dictionary<string, AccessScope>[] maps) =>
        maps.SelectMany(m => m).ToDictionary(kv => kv.Key, kv => kv.Value);
}
