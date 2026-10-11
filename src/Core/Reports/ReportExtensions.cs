namespace Core.Reports;

public static class ReportExtensions
{
    // Returns a deferred query; callers materialize their DTO results once.
    public static IEnumerable<T> TopBy<T>(this IEnumerable<T> source,
        Func<T, decimal> value, Func<T, string> uniqueKey, int n)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(uniqueKey);
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        return source.OrderByDescending(value)
            .ThenBy(uniqueKey, StringComparer.Ordinal).Take(n);
    }
}
