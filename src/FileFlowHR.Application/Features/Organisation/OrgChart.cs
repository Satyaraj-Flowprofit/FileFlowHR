using FileFlowHR.Application.Common;
using FileFlowHR.Application.Common.Messaging;
using FileFlowHR.Application.Common.Security;

namespace FileFlowHR.Application.Features.Organisation;

public sealed record OrgChartNode(PositionRow Position, IReadOnlyList<OrgChartNode> Reports);

/// <summary>The org chart as it was (or is) on a date, built from positions and solid lines.</summary>
[RequiresPermission(Permissions.OrgView)]
public sealed record GetOrgChartQuery(DateOnly? AsOf = null) : IQuery<Result<IReadOnlyList<OrgChartNode>>>;

public sealed class GetOrgChartHandler(IPositionReader reader, TimeProvider clock)
    : IRequestHandler<GetOrgChartQuery, Result<IReadOnlyList<OrgChartNode>>>
{
    public async Task<Result<IReadOnlyList<OrgChartNode>>> Handle(GetOrgChartQuery request, CancellationToken ct)
    {
        var date = request.AsOf ?? clock.Today();
        var positions = (await reader.ListAsync(date, null, null, includeClosed: false, ct)).ToList();
        var ids = positions.Select(p => p.Id).ToHashSet();
        var byManager = positions.Where(p => p.ManagerPositionId is { } m && ids.Contains(m))
            .ToLookup(p => p.ManagerPositionId!.Value);

        OrgChartNode Build(PositionRow p, int depth) =>
            new(p, depth > 50 ? [] : byManager[p.Id].OrderBy(c => c.Title).Select(c => Build(c, depth + 1)).ToList());

        var roots = positions.Where(p => p.ManagerPositionId is null || !ids.Contains(p.ManagerPositionId.Value))
            .OrderBy(p => p.Title).Select(p => Build(p, 0)).ToList();
        return roots;
    }
}
