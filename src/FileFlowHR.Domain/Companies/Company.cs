using FileFlowHR.Domain.Common;

namespace FileFlowHR.Domain.Companies;

public enum CompanyStatus { Active, Suspended, Closed }

/// <summary>A client company. At kickoff this is the tenant: every other table carries its CompanyId.</summary>
public sealed class Company : Entity
{
    private Company() { }

    public string Code { get; private set; } = "";
    public string LegalName { get; private set; } = "";
    public string CountryCode { get; private set; } = "";
    public CompanyStatus Status { get; private set; }

    public static Company Create(string code, string legalName, string countryCode)
    {
        code = Guard.Required(code, "Company code", 30).ToLowerInvariant();
        if (!code.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
            throw new DomainException("Company code may only contain letters, digits and dashes.");

        return new Company
        {
            Code = code,
            LegalName = Guard.Required(legalName, "Legal name", 200),
            CountryCode = Guard.CountryCode(countryCode),
            Status = CompanyStatus.Active,
        };
    }

    public void Rename(string legalName) => LegalName = Guard.Required(legalName, "Legal name", 200);
    public void Suspend() => Status = CompanyStatus.Suspended;
    public void Activate() => Status = CompanyStatus.Active;
}
