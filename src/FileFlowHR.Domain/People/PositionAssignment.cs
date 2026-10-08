using FileFlowHR.Domain.Common;

namespace FileFlowHR.Domain.People;

public enum AssignmentType { Primary, Interim }

/// <summary>Who sits in which position and when. Interim cover must have an end date.</summary>
public sealed class PositionAssignment : EffectiveDatedEntity
{
    private PositionAssignment() { }

    public long PersonId { get; private set; }
    public long PositionId { get; private set; }
    public AssignmentType AssignmentType { get; private set; }

    public static PositionAssignment Create(long personId, long positionId, AssignmentType type,
        DateOnly effectiveFrom, DateOnly? effectiveTo)
    {
        if (type == AssignmentType.Interim && effectiveTo is null)
            throw new DomainException("Interim cover needs an end date.");

        var assignment = new PositionAssignment
        {
            PersonId = personId,
            PositionId = positionId,
            AssignmentType = type,
        };
        assignment.SetPeriod(effectiveFrom, effectiveTo);
        return assignment;
    }
}
