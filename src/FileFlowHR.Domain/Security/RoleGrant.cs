using FileFlowHR.Domain.Common;

namespace FileFlowHR.Domain.Security;

/// <summary>Roles an admin hands out. Employee, Manager and Applicant are worked out, never granted.</summary>
public enum GrantableRole { ClientAdmin, HrAdmin, Recruiter, Auditor }

public enum ScopeType { Company, Region, BusinessUnit }

/// <summary>Who was given which role, over which part of the company, for which dates, by whom.</summary>
public sealed class RoleGrant : EffectiveDatedEntity
{
    private RoleGrant() { }

    public long PersonId { get; private set; }
    public GrantableRole RoleCode { get; private set; }
    public ScopeType ScopeType { get; private set; }
    public long? ScopeId { get; private set; }
    /// <summary>NULL only for a company's first admin, set up during onboarding.</summary>
    public long? GrantedBy { get; private set; }

    public static RoleGrant Create(long personId, GrantableRole role, ScopeType scopeType, long? scopeId,
        DateOnly effectiveFrom, DateOnly? effectiveTo, long? grantedBy)
    {
        if (grantedBy is not null && grantedBy == personId)
            throw new DomainException("Nobody can give a role to themselves.");
        if (scopeType == ScopeType.Company && scopeId is not null)
            throw new DomainException("A company-wide role has no scope ID.");
        if (scopeType != ScopeType.Company && scopeId is null)
            throw new DomainException("Choose the region or business unit this role covers.");

        var grant = new RoleGrant
        {
            PersonId = personId,
            RoleCode = role,
            ScopeType = scopeType,
            ScopeId = scopeId,
            GrantedBy = grantedBy,
        };
        grant.SetPeriod(effectiveFrom, effectiveTo);
        return grant;
    }
}
