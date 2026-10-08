using FileFlowHR.Domain.Common;

namespace FileFlowHR.Domain.Organisation;

/// <summary>A part of the company that follows one country's rules and time zone.</summary>
public sealed class Region : EffectiveDatedEntity
{
    private Region() { }

    public string Name { get; private set; } = "";
    public string CountryCode { get; private set; } = "";
    public string TimeZone { get; private set; } = "";

    public static Region Create(string name, string countryCode, string timeZone, DateOnly effectiveFrom)
    {
        var region = new Region();
        region.Update(name, countryCode, timeZone);
        region.SetPeriod(effectiveFrom, null);
        return region;
    }

    public void Update(string name, string countryCode, string timeZone)
    {
        Name = Guard.Required(name, "Region name", 100);
        CountryCode = Guard.CountryCode(countryCode);
        TimeZone = Guard.Required(timeZone, "Time zone", 64);
    }
}
