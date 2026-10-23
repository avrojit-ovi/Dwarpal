using Dwarpal.Core.Data;
using Dwarpal.Core.Models;
using Dwarpal.Core.Security;

namespace Dwarpal.Core.Tests;

public class ProtectedAppTests
{
    [Fact]
    public async Task AddAndList_RoundTripsApp()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var repository = new ProtectedAppRepository(fixture.Database);
        var exe = @"C:\Program Files\Demo\demo.exe";

        var added = await repository.AddAsync("Demo App", exe);

        Assert.True(added.Id > 0);
        Assert.Equal("Demo App", added.DisplayName);
        Assert.Equal(exe, added.ExecutablePath);
        Assert.Equal("demo", added.ProcessName);
        Assert.True(added.IsEnabled);
        Assert.Null(added.LastLockedUtc);

        var all = await repository.GetAllAsync();
        Assert.Single(all);
        Assert.True(await repository.ExistsAsync(exe));
    }

    [Fact]
    public async Task Add_SamePathDifferentCase_UpdatesInsteadOfDuplicating()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var repository = new ProtectedAppRepository(fixture.Database);

        var first = await repository.AddAsync("Demo", @"C:\Apps\Demo.exe");
        var second = await repository.AddAsync("Demo Renamed", @"c:\apps\demo.EXE");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Demo Renamed", second.DisplayName);
        Assert.Single(await repository.GetAllAsync());
    }

    [Fact]
    public async Task SetEnabled_AndRemove_Work()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var repository = new ProtectedAppRepository(fixture.Database);
        var app = await repository.AddAsync("Demo", @"C:\Apps\demo.exe");

        await repository.SetEnabledAsync(app.Id, false);
        Assert.Empty(await repository.GetAllAsync(enabledOnly: true));
        Assert.Single(await repository.GetAllAsync());

        await repository.MarkLockedAsync(app.Id);
        var reloaded = await repository.GetByIdAsync(app.Id);
        Assert.NotNull(reloaded?.LastLockedUtc);

        Assert.True(await repository.RemoveAsync(app.Id));
        Assert.Empty(await repository.GetAllAsync());
        Assert.False(await repository.RemoveAsync(app.Id));
    }

    [Fact]
    public async Task EnabledOnlyFilter_HidesDisabledApps()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var repository = new ProtectedAppRepository(fixture.Database);

        var enabled = await repository.AddAsync("Enabled", @"C:\Apps\enabled.exe");
        var disabled = await repository.AddAsync("Disabled", @"C:\Apps\disabled.exe");
        await repository.SetEnabledAsync(disabled.Id, false);

        var result = await repository.GetAllAsync(enabledOnly: true);

        Assert.Single(result);
        Assert.Equal(enabled.Id, result[0].Id);
    }

    [Fact]
    public void Matcher_MatchesByExactPath_CaseInsensitive()
    {
        var app = BuildApp(@"C:\Program Files\Demo\demo.exe", "demo");

        Assert.True(ProtectedAppMatcher.Matches(app, "demo.exe", @"c:\program files\demo\DEMO.exe"));

        // Path alada holeo same process name hole match hoy (copy/rename bypass rokhe).
        Assert.True(ProtectedAppMatcher.Matches(app, "demo.exe", @"C:\Other\demo.exe"));
    }

    [Fact]
    public void Matcher_FallsBackToProcessName_WhenPathUnknown()
    {
        var app = BuildApp(@"C:\Program Files\Demo\demo.exe", "demo");

        Assert.True(ProtectedAppMatcher.Matches(app, "Demo.exe", executablePath: null));
        Assert.True(ProtectedAppMatcher.Matches(app, "DEMO.EXE", executablePath: ""));
        Assert.False(ProtectedAppMatcher.Matches(app, "other.exe", executablePath: null));
    }

    [Fact]
    public void Matcher_IgnoresDisabledApps()
    {
        var app = BuildApp(@"C:\Apps\demo.exe", "demo") with { IsEnabled = false };

        Assert.False(ProtectedAppMatcher.Matches(app, "demo.exe", @"C:\Apps\demo.exe"));
    }

    [Fact]
    public void NormalizePath_ExpandsQuotesAndEnvironmentVariables()
    {
        var normalized = ProtectedAppRepository.NormalizePath("  \"C:\\Apps\\demo.exe\"  ");

        Assert.Equal(@"C:\Apps\demo.exe", normalized);
    }

    private static ProtectedApp BuildApp(string path, string processName) => new(
        1, "Demo", path, processName, true, DateTimeOffset.UtcNow, null);
}
