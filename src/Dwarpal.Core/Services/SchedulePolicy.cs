using Dwarpal.Core.Models;

namespace Dwarpal.Core.Services;

/// <summary>
/// Schedule evaluation. Kono enabled window na thakle protection always on.
/// Window start == end allowed na (validation), start &gt; end mane midnight cross kore.
/// </summary>
public static class SchedulePolicy
{
    public static bool HasEnabledWindow(IReadOnlyList<ProtectionSchedule> schedules)
        => schedules.Any(schedule => schedule.IsEnabled);

    public static bool IsProtectionActive(
        IReadOnlyList<ProtectionSchedule> schedules,
        DateTimeOffset localNow)
    {
        if (!HasEnabledWindow(schedules))
        {
            return true;
        }

        var minutes = localNow.Hour * 60 + localNow.Minute;
        var today = (int)localNow.DayOfWeek; // Sunday = 0
        var yesterday = (today + 6) % 7;

        foreach (var schedule in schedules)
        {
            if (!schedule.IsEnabled)
            {
                continue;
            }

            var startsToday = (schedule.DaysMask & (1 << today)) != 0;
            var startedYesterday = (schedule.DaysMask & (1 << yesterday)) != 0;

            if (schedule.StartMinutes < schedule.EndMinutes)
            {
                if (startsToday && minutes >= schedule.StartMinutes && minutes < schedule.EndMinutes)
                {
                    return true;
                }
            }
            else
            {
                // Overnight: aaj start holo, othoba kal start hoye aaj end hobe.
                if (startsToday && minutes >= schedule.StartMinutes)
                {
                    return true;
                }

                if (startedYesterday && minutes < schedule.EndMinutes)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>UI test er jonno: porer window kobe shuru hobe (aj theke 7 din ag porjonto).</summary>
    public static TimeSpan? TimeUntilNextWindow(
        IReadOnlyList<ProtectionSchedule> schedules,
        DateTimeOffset localNow)
    {
        var enabled = schedules.Where(schedule => schedule.IsEnabled).ToArray();

        if (enabled.Length == 0 || IsProtectionActive(schedules, localNow))
        {
            return null;
        }

        // localNow er wall-clock time use koro; LocalDateTime machine timezone e convert kore fele.
        var wallClock = localNow.DateTime;

        for (var dayOffset = 0; dayOffset <= 7; dayOffset++)
        {
            var day = wallClock.Date.AddDays(dayOffset);

            foreach (var schedule in enabled)
            {
                var candidate = day.AddMinutes(schedule.StartMinutes);

                if (candidate <= wallClock)
                {
                    continue;
                }

                var candidateDay = (int)candidate.DayOfWeek;

                if ((schedule.DaysMask & (1 << candidateDay)) == 0)
                {
                    continue;
                }

                return candidate - wallClock;
            }
        }

        return null;
    }
}
