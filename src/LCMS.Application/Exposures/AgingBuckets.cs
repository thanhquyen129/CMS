namespace LCMS.Application.Exposures;

/// <summary>
/// Derived AP/AR aging (ADR-0006). Not stored — computed from due date vs as-of.
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

    public static (int? DaysPastDue, string Bucket) Classify(DateOnly? dueDate, DateOnly asOf)
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

        if (days <= 30)
        {
            return (days, Days1To30);
        }

        if (days <= 60)
        {
            return (days, Days31To60);
        }

        if (days <= 90)
        {
            return (days, Days61To90);
        }

        return (days, Days90Plus);
    }
}
