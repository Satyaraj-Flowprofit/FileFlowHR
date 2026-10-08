using FileFlowHR.Domain.Common;

namespace FileFlowHR.Domain.Organisation;

/// <summary>A department or division inside a region. It can sit under another business unit.</summary>
public sealed class BusinessUnit : EffectiveDatedEntity
{
    private BusinessUnit() { }

    public long RegionId { get; private set; }
    public long? ParentBusinessUnitId { get; private set; }
    public string Name { get; private set; } = "";

    public static BusinessUnit Create(long regionId, long? parentBusinessUnitId, string name, DateOnly effectiveFrom)
    {
        var unit = new BusinessUnit { RegionId = regionId };
        unit.Update(parentBusinessUnitId, name);
        unit.SetPeriod(effectiveFrom, null);
        return unit;
    }

    public void Update(long? parentBusinessUnitId, string name)
    {
        if (parentBusinessUnitId is not null && parentBusinessUnitId == Id && Id != 0)
            throw new DomainException("A business unit cannot sit under itself.");
        ParentBusinessUnitId = parentBusinessUnitId;
        Name = Guard.Required(name, "Business unit name", 150);
    }
}
