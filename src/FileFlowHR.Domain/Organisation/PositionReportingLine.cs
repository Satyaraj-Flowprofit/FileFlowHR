using FileFlowHR.Domain.Common;

namespace FileFlowHR.Domain.Organisation;

public enum LineType { Solid, Dotted }

/// <summary>
/// Position reports to manager position. Solid is the main boss and drives the Manager role;
/// dotted gives the other manager view access to that one position.
/// </summary>
public sealed class PositionReportingLine : EffectiveDatedEntity
{
    private PositionReportingLine() { }

    public long PositionId { get; private set; }
    public long ManagerPositionId { get; private set; }
    public LineType LineType { get; private set; }

    public static PositionReportingLine Create(long positionId, long managerPositionId, LineType lineType, DateOnly effectiveFrom)
    {
        if (positionId == managerPositionId)
            throw new DomainException("A position cannot report to itself.");
        var line = new PositionReportingLine
        {
            PositionId = positionId,
            ManagerPositionId = managerPositionId,
            LineType = lineType,
        };
        line.SetPeriod(effectiveFrom, null);
        return line;
    }
}
