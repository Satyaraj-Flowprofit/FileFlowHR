namespace FileFlowHR.Application.Common;

public static class ClockExtensions
{
    public static DateOnly Today(this TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
    public static DateTime UtcNow(this TimeProvider clock) => clock.GetUtcNow().UtcDateTime;
}

public static class DateQueries
{
    /// <summary>Readable "as of" check, matching the database rule: from inclusive, to exclusive.</summary>
    public static bool ActiveOn(DateOnly from, DateOnly? to, DateOnly date) => from <= date && (to == null || to > date);
}
