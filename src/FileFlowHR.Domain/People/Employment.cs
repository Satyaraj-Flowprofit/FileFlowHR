using FileFlowHR.Domain.Common;

namespace FileFlowHR.Domain.People;

public enum EmploymentType { FullTime, PartTime, Temporary }

/// <summary>A period of being employed. An open row (no TerminationDate) means "is an employee now".</summary>
public sealed class Employment : CompanyOwnedEntity
{
    private Employment() { }

    public long PersonId { get; private set; }
    public DateOnly HireDate { get; private set; }
    /// <summary>Exclusive: the first day the person is no longer employed.</summary>
    public DateOnly? TerminationDate { get; private set; }
    public EmploymentType EmploymentType { get; private set; }

    public bool IsActiveOn(DateOnly date) =>
        HireDate <= date && (TerminationDate is null || TerminationDate > date);

    public static Employment Hire(long personId, DateOnly hireDate, EmploymentType type) => new()
    {
        PersonId = personId,
        HireDate = hireDate,
        EmploymentType = type,
    };

    public void Terminate(DateOnly terminationDate)
    {
        if (TerminationDate is not null)
            throw new DomainException("This employment has already ended.");
        if (terminationDate <= HireDate)
            throw new DomainException("The leaving date must be after the hire date.");
        TerminationDate = terminationDate;
    }
}
