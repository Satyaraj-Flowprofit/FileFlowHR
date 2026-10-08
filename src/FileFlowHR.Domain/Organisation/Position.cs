using FileFlowHR.Domain.Common;

namespace FileFlowHR.Domain.Organisation;

/// <summary>One seat in the org chart. It exists whether or not someone sits in it.</summary>
public sealed class Position : EffectiveDatedEntity
{
    private Position() { }

    public string Code { get; private set; } = "";
    public string Title { get; private set; } = "";
    public long BusinessUnitId { get; private set; }
    public long? LocationId { get; private set; }

    public static Position Create(string code, string title, long businessUnitId, long? locationId, DateOnly effectiveFrom)
    {
        var position = new Position { Code = Guard.Required(code, "Position code", 30).ToUpperInvariant() };
        position.Update(title, businessUnitId, locationId);
        position.SetPeriod(effectiveFrom, null);
        return position;
    }

    public void Update(string title, long businessUnitId, long? locationId)
    {
        Title = Guard.Required(title, "Title", 150);
        BusinessUnitId = businessUnitId;
        LocationId = locationId;
    }
}
