using Microsoft.AspNetCore.Identity;

namespace FileFlowHR.Infrastructure.Identity;

/// <summary>
/// A sign-in account (ASP.NET Core Identity user) that belongs to exactly one company.
/// UserName is "companycode:email" so the same email can exist at two companies.
/// PasswordHash is empty for staff of companies that use their own login system.
/// </summary>
public sealed class UserAccount : IdentityUser<long>
{
    public long CompanyId { get; set; }
    public long? PersonId { get; set; }
    public string UserType { get; set; } = UserTypes.Internal;
    public string Status { get; set; } = UserStatuses.Invited;
    public DateTime? LastSignInAt { get; set; }

    public static string MakeUserName(string companyCode, string email) =>
        $"{companyCode.Trim().ToLowerInvariant()}:{email.Trim().ToLowerInvariant()}";
}

public static class UserTypes
{
    public const string Internal = "internal";
    public const string External = "external";
}

public static class UserStatuses
{
    public const string Invited = "invited";
    public const string Active = "active";
    public const string Disabled = "disabled";
}

public sealed class UserExternalLogin : IdentityUserLogin<long>;
public sealed class UserToken : IdentityUserToken<long>;
public sealed class UserClaim : IdentityUserClaim<long>;
