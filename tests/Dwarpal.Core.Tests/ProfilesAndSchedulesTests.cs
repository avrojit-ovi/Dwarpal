using Dwarpal.Core.Data;
using Dwarpal.Core.Models;
using Dwarpal.Core.Services;

namespace Dwarpal.Core.Tests;

public class ProfilesAndSchedulesTests
{
    [Fact]
    public async Task Profile_AssignApps_AndActivate_ScopesProtection()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var apps = new ProtectedAppRepository(fixture.Database);
        var first = await apps.AddAsync("Editor", @"C:\Apps\editor.exe");
        var second = await apps.AddAsync("Browser", @"C:\Apps\browser.exe");

        var profile = await fixture.Profiles.CreateAsync("Work");
        await fixture.Profiles.SetAppsAsync(profile.Id, new[] { first.Id });

        Assert.Equal(new[] { first.Id }, await fixture.Profiles.GetAppIdsAsync(profile.Id));
        Assert.Null(await fixture.Profiles.GetActiveAppIdsAsync());

        await fixture.Profiles.SetActiveProfileIdAsync(profile.Id);

        Assert.Equal(profile.Id, await fixture.Profiles.GetActiveProfileIdAsync());
        var active = await fixture.Profiles.GetActiveAppIdsAsync();
        Assert.NotNull(active);
        Assert.Contains(first.Id, active!);
        Assert.DoesNotContain(second.Id, active!);

        // App delete hole profile assignment o cascade kore chole jay.
        await apps.RemoveAsync(first.Id);
        Assert.Empty(await fixture.Profiles.GetAppIdsAsync(profile.Id));

        await fixture.Profiles.SetActiveProfileIdAsync(Profile.AllAppsProfileId);
        Assert.Null(await fixture.Profiles.GetActiveAppIdsAsync());
    }

    [Fact]
    public async Task DeleteActiveProfile_FallsBackToAllApps()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var profile = await fixture.Profiles.CreateAsync("Gaming");
        await fixture.Profiles.SetActiveProfileIdAsync(profile.Id);

        Assert.True(await fixture.Profiles.DeleteAsync(profile.Id));
        Assert.Equal(Profile.AllAppsProfileId, await fixture.Profiles.GetActiveProfileIdAsync());
    }

    [Fact]
    public async Task ProfileNames_AreValidatedAndUnique()
    {
        await using var fixture = await TestDatabase.CreateAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Profiles.CreateAsync("  "));
        await Assert.ThrowsAsync<ArgumentException>(
            () => fixture.Profiles.CreateAsync(new string('x', Profile.MaxNameLength + 1)));

        await fixture.Profiles.CreateAsync("Work");
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Profiles.CreateAsync(" work "));
    }

    [Fact]
    public async Task DeletingProfile_RemovesAppAssignments()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var apps = new ProtectedAppRepository(fixture.Database);
        var app = await apps.AddAsync("Editor", @"C:\Apps\editor.exe");
        var profile = await fixture.Profiles.CreateAsync("Work");
        await fixture.Profiles.SetAppsAsync(profile.Id, new[] { app.Id });

        await fixture.Profiles.DeleteAsync(profile.Id);

        await using var connection = await fixture.Database.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM ProfileApps;";
        Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    [Fact]
    public void Schedule_Validation_RejectsBadWindows()
    {
        Assert.NotNull(ProtectionSchedule.Validate("", ProtectionSchedule.AllDaysMask, 540, 1080));
        Assert.NotNull(ProtectionSchedule.Validate("Work", 0, 540, 1080));
        Assert.NotNull(ProtectionSchedule.Validate("Work", ProtectionSchedule.AllDaysMask, 540, 540));
        Assert.NotNull(ProtectionSchedule.Validate("Work", ProtectionSchedule.AllDaysMask, -1, 1080));
        Assert.NotNull(ProtectionSchedule.Validate("Work", ProtectionSchedule.AllDaysMask, 540, 1440));
        Assert.Null(ProtectionSchedule.Validate("Work", ProtectionSchedule.AllDaysMask, 540, 1080));
        Assert.Null(ProtectionSchedule.Validate("Night", 0b0100_0001, 1320, 120));
    }

    [Fact]
    public void NoEnabledWindows_MeansAlwaysActive()
    {
        var none = Array.Empty<ProtectionSchedule>();
        var monday = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

        Assert.True(SchedulePolicy.IsProtectionActive(none, monday));

        var disabled = new[]
        {
            new ProtectionSchedule(1, "Work", ProtectionSchedule.AllDaysMask, 540, 1080, false, monday),
        };

        Assert.True(SchedulePolicy.IsProtectionActive(disabled, monday));
    }

    [Fact]
    public void DaytimeWindow_RespectsStartAndEndBoundaries()
    {
        var mondayWindow = new ProtectionSchedule(
            1, "Work", Days("Mon", "Tue", "Wed", "Thu", "Fri"), 540, 1080, true, DateTimeOffset.UtcNow);

        Assert.False(SchedulePolicy.IsProtectionActive(
            new[] { mondayWindow }, new DateTimeOffset(2026, 10, 26, 8, 59, 0, TimeSpan.Zero)));
        Assert.True(SchedulePolicy.IsProtectionActive(
            new[] { mondayWindow }, new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero)));
        Assert.True(SchedulePolicy.IsProtectionActive(
            new[] { mondayWindow }, new DateTimeOffset(2026, 10, 26, 17, 59, 0, TimeSpan.Zero)));
        Assert.False(SchedulePolicy.IsProtectionActive(
            new[] { mondayWindow }, new DateTimeOffset(2026, 10, 26, 18, 0, 0, TimeSpan.Zero)));

        // Saturday te window nei.
        Assert.False(SchedulePolicy.IsProtectionActive(
            new[] { mondayWindow }, new DateTimeOffset(2026, 10, 31, 10, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void OvernightWindow_WrapsIntoNextDay()
    {
        var night = new ProtectionSchedule(
            1, "Night", Days("Sat"), 1320, 120, true, DateTimeOffset.UtcNow);

        // Saturday 23:30 - same day start.
        Assert.True(SchedulePolicy.IsProtectionActive(
            new[] { night }, new DateTimeOffset(2026, 10, 31, 23, 30, 0, TimeSpan.Zero)));

        // Sunday 01:00 - Saturday er window midnight cross koreche.
        Assert.True(SchedulePolicy.IsProtectionActive(
            new[] { night }, new DateTimeOffset(2026, 11, 1, 1, 0, 0, TimeSpan.Zero)));

        // Sunday 03:00 - window sesh.
        Assert.False(SchedulePolicy.IsProtectionActive(
            new[] { night }, new DateTimeOffset(2026, 11, 1, 3, 0, 0, TimeSpan.Zero)));

        // Monday 01:00 - Sunday te window shuru hoy ni.
        Assert.False(SchedulePolicy.IsProtectionActive(
            new[] { night }, new DateTimeOffset(2026, 11, 2, 1, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void TimeUntilNextWindow_ReportsNextStart()
    {
        var window = new ProtectionSchedule(
            1, "Work", ProtectionSchedule.AllDaysMask, 540, 1080, true, DateTimeOffset.UtcNow);
        var evening = new DateTimeOffset(2026, 10, 26, 19, 0, 0, TimeSpan.Zero);

        var next = SchedulePolicy.TimeUntilNextWindow(new[] { window }, evening);

        Assert.NotNull(next);
        Assert.Equal(TimeSpan.FromHours(14), next);
    }

    [Fact]
    public async Task ScheduleRepository_RoundTripsWindows()
    {
        await using var fixture = await TestDatabase.CreateAsync();

        var created = await fixture.Schedules.AddAsync("Work", Days("Mon", "Tue"), 540, 1080);
        Assert.Equal(540, created.StartMinutes);
        Assert.Equal("09:00–18:00", created.TimeRangeText);
        Assert.Equal("Mon, Tue", created.DaysText);

        var all = await fixture.Schedules.GetAllAsync();
        Assert.Single(all);

        await fixture.Schedules.SetEnabledAsync(created.Id, false);
        Assert.False((await fixture.Schedules.GetAllAsync()).Single().IsEnabled);

        Assert.True(await fixture.Schedules.DeleteAsync(created.Id));
        Assert.Empty(await fixture.Schedules.GetAllAsync());
    }

    private static int Days(params string[] names)
    {
        var all = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        var mask = 0;

        foreach (var name in names)
        {
            mask |= 1 << Array.IndexOf(all, name);
        }

        return mask;
    }
}
