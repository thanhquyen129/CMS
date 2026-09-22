using LCMS.Domain.Entities;

namespace LCMS.Application.Exposures;

/// <summary>
/// Derived AP/AR aging (ADR-0006 / CR-27). Boundaries come from tenant settings when set.
/// </summary>
public static class AgingBuckets
{
    public const string NoDueDate = "no_due_date";
    public const string Current = "current";
    public const string Days1To30 = "1_30";
    public const string Days31To60 = "31_60";
    public const string Days61To90 = "61_90";
    public const string Days90Plus = "90_plus";

    public static readonly IReadOnlyList<string> Ordered =
    [
        NoDueDate,
        Current,
        Days1To30,
        Days31To60,
        Days61To90,
        Days90Plus
    ];

    public static AgingBucketBounds DefaultBounds { get; } = new(30, 60, 90);

    public static AgingBucketBounds ResolveBounds(TenantFinancialSettings? settings)
    {
        var b1 = settings?.AgingBucket1Days is > 0 ? settings.AgingBucket1Days.Value : 30;
        var b2 = settings?.AgingBucket2Days is > 0 ? settings.AgingBucket2Days.Value : 60;
        var b3 = settings?.AgingBucket3Days is > 0 ? settings.AgingBucket3Days.Value : 90;
        if (b2 < b1)
        {
            b2 = b1;
        }

        if (b3 < b2)
        {
            b3 = b2;
        }

        return new AgingBucketBounds(b1, b2, b3);
    }

    public static (int? DaysPastDue, string Bucket) Classify(DateOnly? dueDate, DateOnly asOf) =>
        Classify(dueDate, asOf, DefaultBounds);

    public static (int? DaysPastDue, string Bucket) Classify(
        DateOnly? dueDate,
        DateOnly asOf,
        AgingBucketBounds bounds)
    {
        if (!dueDate.HasValue)
        {
            return (null, NoDueDate);
        }

        var days = asOf.DayNumber - dueDate.Value.DayNumber;
        if (days <= 0)
        {
            return (days, Current);
        }

        if (days <= bounds.Bucket1Days)
        {
            return (days, Days1To30);
        }

        if (days <= bounds.Bucket2Days)
        {
            return (days, Days31To60);
        }

        if (days <= bounds.Bucket3Days)
        {
            return (days, Days61To90);
        }

        return (days, Days90Plus);
    }
}

public readonly record struct AgingBucketBounds(int Bucket1Days, int Bucket2Days, int Bucket3Days);
