namespace FileFlowHR.Domain.Organisation;

/// <summary>Walks solid reporting lines. Pure logic, so it is easy to test.</summary>
public static class OrgTree
{
    /// <summary>All positions below the given ones, following solid lines, any depth.</summary>
    public static HashSet<long> Descendants(IEnumerable<long> rootPositionIds,
        IEnumerable<(long PositionId, long ManagerPositionId)> solidLines)
    {
        var children = solidLines
            .GroupBy(l => l.ManagerPositionId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.PositionId).ToList());

        var result = new HashSet<long>();
        var queue = new Queue<long>(rootPositionIds);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!children.TryGetValue(current, out var kids)) continue;
            foreach (var kid in kids)
                if (result.Add(kid)) queue.Enqueue(kid);
        }
        return result;
    }

    /// <summary>True if making child report to manager would create a loop.</summary>
    public static bool WouldCreateCycle(long childPositionId, long managerPositionId,
        IEnumerable<(long PositionId, long ManagerPositionId)> solidLines) =>
        childPositionId == managerPositionId ||
        Descendants([childPositionId], solidLines).Contains(managerPositionId);
}
