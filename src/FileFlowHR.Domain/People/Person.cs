using FileFlowHR.Domain.Common;

namespace FileFlowHR.Domain.People;

/// <summary>
/// One row per human per company: staff, former staff and applicants alike. Whether someone is
/// an employee, manager or applicant comes from Employment, assignments and applications.
/// </summary>
public sealed class Person : CompanyOwnedEntity
{
    private Person() { }

    public string PersonNumber { get; private set; } = "";
    public string FirstName { get; private set; } = "";
    public string LastName { get; private set; } = "";
    public string? Email { get; private set; }
    public string? Phone { get; private set; }

    public string FullName => $"{FirstName} {LastName}";

    public static Person Create(string personNumber, string firstName, string lastName, string? email, string? phone)
    {
        var person = new Person { PersonNumber = Guard.Required(personNumber, "Person number", 20).ToUpperInvariant() };
        person.Update(firstName, lastName, email, phone);
        return person;
    }

    public void Update(string firstName, string lastName, string? email, string? phone)
    {
        FirstName = Guard.Required(firstName, "First name", 100);
        LastName = Guard.Required(lastName, "Last name", 100);
        Email = Guard.Optional(email, "Email", 320)?.ToLowerInvariant();
        Phone = Guard.Optional(phone, "Phone", 30);
    }
}
