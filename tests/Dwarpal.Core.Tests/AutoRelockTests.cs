using Dwarpal.Core.Data;
using Dwarpal.Core.Models;
using Dwarpal.Core.Security;
using Dwarpal.Core.Services;

namespace Dwarpal.Core.Tests;

public class AutoRelockTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRelock_ReturnsFalse_WhenAppNeverBackgrounded()
    {
        Assert.False(AutoRelockPolicy.ShouldRelock(Now, Now.AddMinutes(-10), null, 1, 5));
    }

    [Fact]
    public void ShouldRelock_ReturnsFalse_WhenTimeoutDisabled()
    {
        Assert.False(AutoRelockPolicy.ShouldRelock(Now, Now.AddMinutes(-10), Now.AddMinutes(-10), null, 5));
        Assert.False(AutoRelockPolicy.ShouldRelock(Now, Now.AddMinutes(-10), Now.AddMinutes(-10), 0, 5));
    }

    [Fact]
    public void ShouldRelock_RespectsTimeout()
    {
        var unlocked = Now.AddMinutes(-10);

        Assert.False(AutoRelockPolicy.ShouldRelock(Now, unlocked, Now.AddSeconds(-50), 1, 5));
        Assert.True(AutoRelockPolicy.ShouldRelock(Now, unlocked, Now.AddSeconds(-61), 1, 5));
    }

    [Fact]
    public void ShouldRelock_RespectsGracePeriod()
    {
        // Unlock er matro 2 second holo, grace 10 second.
        Assert.False(AutoRelockPolicy.ShouldRelock(Now, Now.AddSeconds(-2), Now.AddMinutes(-5), 1, 10));

        Assert.True(AutoRelockPolicy.ShouldRelock(Now, Now.AddSeconds(-11), Now.AddMinutes(-5), 1, 10));
    }

    [Fact]
    public void ResolveTimeout_UsesOverrideAndGlobalDefault()
    {
        var settings = new AutoRelockSettings(5, 5);

        Assert.Equal(5, settings.ResolveTimeout(BuildApp(null)));
        Assert.Equal(15, settings.ResolveTimeout(BuildApp(15)));
        Assert.Null(settings.ResolveTimeout(BuildApp(AutoRelockSettings.Never)));
    }

    [Fact]
    public void ResolveTimeout_GlobalNever_DisablesEverything()
    {
        var settings = new AutoRelockSettings(AutoRelockSettings.Never, 5);

        Assert.Null(settings.ResolveTimeout(BuildApp(null)));
        Assert.Equal(30, settings.ResolveTimeout(BuildApp(30)));
    }

    [Fact]
    public async Task AppSettingsService_RoundTripsAndClampsValues()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var service = new AppSettingsService(fixture.Settings);

        var defaults = await service.GetAutoRelockAsync();
        Assert.Equal(AutoRelockSettings.DefaultMinutesValue, defaults.DefaultMinutes);
        Assert.Equal(AutoRelockSettings.DefaultGraceSeconds, defaults.GraceSeconds);

        await service.SaveAutoRelockAsync(new AutoRelockSettings(15, 30));
        var reloaded = await service.GetAutoRelockAsync();
        Assert.Equal(15, reloaded.DefaultMinutes);
        Assert.Equal(30, reloaded.GraceSeconds);

        await service.SaveAutoRelockAsync(new AutoRelockSettings(0, -10));
        var clamped = await service.GetAutoRelockAsync();
        Assert.Equal(AutoRelockSettings.DefaultMinutesValue, clamped.DefaultMinutes);
        Assert.Equal(0, clamped.GraceSeconds);
    }

    [Fact]
    public async Task ProtectedAppRepository_StoresPerAppAutoRelock()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var repository = new ProtectedAppRepository(fixture.Database);
        var app = await repository.AddAsync("Demo", @"C:\Apps\demo.exe");

        Assert.Null(app.AutoRelockMinutes);

        await repository.SetAutoRelockAsync(app.Id, 15);
        Assert.Equal(15, (await repository.GetByIdAsync(app.Id))?.AutoRelockMinutes);

        await repository.SetAutoRelockAsync(app.Id, AutoRelockSettings.Never);
        Assert.Equal(AutoRelockSettings.Never, (await repository.GetByIdAsync(app.Id))?.AutoRelockMinutes);

        await repository.SetAutoRelockAsync(app.Id, null);
        Assert.Null((await repository.GetByIdAsync(app.Id))?.AutoRelockMinutes);
    }

    private static ProtectedApp BuildApp(int? overrideMinutes) => new(
        1, "Demo", @"C:\Apps\demo.exe", "demo", true, Now, null, overrideMinutes);
}
