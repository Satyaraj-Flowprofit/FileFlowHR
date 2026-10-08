namespace FileFlowHR.Domain.Common;

/// <summary>
/// A row that is true for a period. EffectiveFrom is inclusive, EffectiveTo is exclusive and
/// NULL means "still true". Rows are ended, never deleted.
/// </summary>
public abstract class EffectiveDatedEntity : CompanyOwnedEntity
{
    public DateOnly EffectiveFrom { get; protected set; }
    public DateOnly? EffectiveTo { get; protected set; }

    public bool IsOpen => EffectiveTo is null;

    public bool IsActiveOn(DateOnly date) =>
        EffectiveFrom <= date && (EffectiveTo is null || EffectiveTo > date);

    public void EndOn(DateOnly endDate)
    {
        if (EffectiveTo is not null)
            throw new DomainException("This record has already ended.");
        if (endDate <= EffectiveFrom)
            throw new DomainException("The end date must be after the start date.");
        EffectiveTo = endDate;
    }

    protected void SetPeriod(DateOnly from, DateOnly? to)
    {
        if (to is not null && to <= from)
            throw new DomainException("The end date must be after the start date.");
        EffectiveFrom = from;
        EffectiveTo = to;
    }
}
