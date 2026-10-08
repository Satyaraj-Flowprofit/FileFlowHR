using FileFlowHR.Domain.Common;

namespace FileFlowHR.Domain.Organisation;

/// <summary>A physical place people work.</summary>
public sealed class Location : CompanyOwnedEntity
{
    private Location() { }

    public long RegionId { get; private set; }
    public string Name { get; private set; } = "";
    public string? City { get; private set; }
    public string CountryCode { get; private set; } = "";

    public static Location Create(long regionId, string name, string? city, string countryCode)
    {
        var location = new Location { RegionId = regionId };
        location.Update(name, city, countryCode);
        return location;
    }

    public void Update(string name, string? city, string countryCode)
    {
        Name = Guard.Required(name, "Location name", 150);
        City = Guard.Optional(city, "City", 100);
        CountryCode = Guard.CountryCode(countryCode);
    }
}
