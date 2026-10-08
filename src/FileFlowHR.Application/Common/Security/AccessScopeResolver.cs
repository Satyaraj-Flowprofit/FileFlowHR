using FileFlowHR.Application.Abstractions;
using FileFlowHR.Domain.Organisation;
using Microsoft.EntityFrameworkCore;

namespace FileFlowHR.Application.Common.Security;

public interface IAccessScopeResolver
{
    /// <summary>Positions the user may act on for this permission. NULL means every position in the company.</summary>
    Task<HashSet<long>?> PositionIdsAsync(string permission, CancellationToken ct = default);

    /// <summary>People the user may act on for this permission. NULL means everyone in the company.</summary>
    Task<HashSet<long>?> PersonIdsAsync(string permission, CancellationToken ct = default);
}

/// <summary>
/// Turns a permission's reach (region, business unit, team, self) into concrete record IDs,
/// so list screens show only what the user may see.
/// </summary>
public sealed class AccessScopeResolver(IAppDbContext db, IPermissionService permissions, ICurrentUser user, TimeProvider clock)
    : IAccessScopeResolver
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    public async Task<HashSet<long>?> PositionIdsAsync(string permission, CancellationToken ct = default)
    {
        var reach = permissions.Reach(permission);
        if (reach.Company) return null;
        var result = new HashSet<long>();
        if (!reach.Any) return result;

        if (reach.RegionIds.Count > 0 || reach.BusinessUnitIds.Count > 0)
        {
            var units = await db.BusinessUnits.AsNoTracking()
                .Select(b => new { b.Id, b.RegionId, b.ParentBusinessUnitId }).ToListAsync(ct);
            var visibleUnits = units.Where(u => reach.RegionIds.Contains(u.RegionId)).Select(u => u.Id).ToHashSet();
            // A business-unit grant covers that unit and every unit beneath it.
            var queue = new Queue<long>(reach.BusinessUnitIds);
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!visibleUnits.Add(id) && !reach.BusinessUnitIds.Contains(id)) continue;
                foreach (var child in units.Where(u => u.ParentBusinessUnitId == id)) queue.Enqueue(child.Id);
            }
            var ids = await db.Positions.AsNoTracking()
                .Where(p => visibleUnits.Contains(p.BusinessUnitId)).Select(p => p.Id).ToListAsync(ct);
            result.UnionWith(ids);
        }

        if ((reach.Team || reach.Self) && user.PersonId is { } personId)
        {
            var today = Today;
            var held = await db.PositionAssignments.AsNoTracking()
                .Where(a => a.PersonId == personId && a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo > today))
                .Select(a => a.PositionId).ToListAsync(ct);
            result.UnionWith(held);

            if (reach.Team)
            {
                var lines = await db.PositionReportingLines.AsNoTracking()
                    .Where(l => l.EffectiveFrom <= today && (l.EffectiveTo == null || l.EffectiveTo > today))
                    .Select(l => new { l.PositionId, l.ManagerPositionId, l.LineType }).ToListAsync(ct);
                result.UnionWith(OrgTree.Descendants(held,
                    lines.Where(l => l.LineType == LineType.Solid).Select(l => (l.PositionId, l.ManagerPositionId))));
                // Dotted line: the manager may view that one position too.
                result.UnionWith(lines.Where(l => l.LineType == LineType.Dotted && held.Contains(l.ManagerPositionId))
                    .Select(l => l.PositionId));
            }
        }
        return result;
    }

    public async Task<HashSet<long>?> PersonIdsAsync(string permission, CancellationToken ct = default)
    {
        var positions = await PositionIdsAsync(permission, ct);
        if (positions is null) return null;

        var today = Today;
        var people = await db.PositionAssignments.AsNoTracking()
            .Where(a => positions.Contains(a.PositionId) && a.EffectiveFrom <= today && (a.EffectiveTo == null || a.EffectiveTo > today))
            .Select(a => a.PersonId).ToListAsync(ct);
        var result = people.ToHashSet();
        if (permissions.Reach(permission).Self && user.PersonId is { } self) result.Add(self);
        return result;
    }
}
