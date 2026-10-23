namespace Dwarpal.Core.Models;

/// <summary>
/// Protection window. DaysMask bit 0 = Sunday ... bit 6 = Saturday.
/// StartMinutes/EndMinutes local time er minute-of-day; start &gt; end hole window midnight cross kore.
/// </summary>
public sealed record ProtectionSchedule(
    int Id,
    string Name,
    int DaysMask,
    int StartMinutes,
    int EndMinutes,
    bool IsEnabled,
    DateTimeOffset CreatedUtc)
{
    public const int MaxNameLength = 40;
    public const int AllDaysMask = 0b111_1111;
    public const int MinutesPerDay = 24 * 60;

    public static string? Validate(string? name, int daysMask, int startMinutes, int endMinutes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Window er naam dao.";
        }

        if (name.Trim().Length > MaxNameLength)
        {
            return $"Naam {MaxNameLength} character er beshi hote parbe na.";
        }

        if (daysMask is <= 0 or > AllDaysMask)
        {
            return "Onto ekta din select koro.";
        }

        if (startMinutes is < 0 or >= MinutesPerDay || endMinutes is < 0 or >= MinutesPerDay)
        {
            return "Somoy 00:00 theke 23:59 er moddhe hote hobe.";
        }

        return startMinutes == endMinutes
            ? "Start ar end somoy ek hote parbe na."
            : null;
    }

    public string TimeRangeText => $"{FormatMinutes(StartMinutes)}–{FormatMinutes(EndMinutes)}";

    public string DaysText
    {
        get
        {
            if (DaysMask == AllDaysMask)
            {
                return "Every day";
            }

            if (DaysMask == 0b001_1111)
            {
                return "Mon–Fri";
            }

            if (DaysMask == 0b110_0000)
            {
                return "Sat–Sun";
            }

            var names = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
            var days = names.Where((_, index) => (DaysMask & (1 << index)) != 0);

            return string.Join(", ", days);
        }
    }

    public static string FormatMinutes(int minutes)
    {
        var clamped = Math.Clamp(minutes, 0, MinutesPerDay - 1);
        return $"{clamped / 60:00}:{clamped % 60:00}";
    }
}
