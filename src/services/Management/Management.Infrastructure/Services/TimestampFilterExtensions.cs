using Management.Application.Common;
using Management.Infrastructure.Persistence;

namespace Management.Infrastructure.Services;

internal static class TimestampFilterExtensions
{
    public static IQueryable<T> WhereCreatedBetween<T>(this IQueryable<T> query, DateTime? from, DateTime? to)
        where T : class, ITimestampedEntity
    {
        DateTime? fromUtc = from is { } f ? NormalizeUtc(f) : null;
        DateTime? toUtc = to is { } t
            ? (t.TimeOfDay == TimeSpan.Zero ? NormalizeUtc(t).AddDays(1).AddTicks(-1) : NormalizeUtc(t))
            : null;
        if (fromUtc is not null && toUtc is not null && fromUtc > toUtc)
            throw ApiException.Validation("createdAtFrom", "CreatedAtFrom must be less than or equal to CreatedAtTo.");
        if (fromUtc is not null) query = query.Where(e => e.CreatedAt >= fromUtc.Value);
        if (toUtc is not null) query = query.Where(e => e.CreatedAt <= toUtc.Value);
        return query;
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
