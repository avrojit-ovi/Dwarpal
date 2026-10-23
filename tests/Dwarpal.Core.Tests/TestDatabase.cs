using Dwarpal.Core.Data;
using Dwarpal.Core.Security;
using Dwarpal.Core.Services;

namespace Dwarpal.Core.Tests;

/// <summary>
/// Prottek test er jonno alada temp SQLite file, jate test gulo ek arek tar data na dekhe.
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private readonly string _root;

    private TestDatabase(string root)
    {
        _root = root;
        Database = new DwarpalDatabase(Path.Combine(root, "dwarpal-test.db"));
    }

    public DwarpalDatabase Database { get; }
    public ActivityRepository Activity { get; private set; } = null!;
    public SettingsRepository Settings { get; private set; } = null!;
    public CredentialRepository Credentials { get; private set; } = null!;
    public ProfileRepository Profiles { get; private set; } = null!;
    public ScheduleRepository Schedules { get; private set; } = null!;
    public LockoutService Lockout { get; private set; } = null!;
    public CredentialService CredentialService { get; private set; } = null!;

    public static async Task<TestDatabase> CreateAsync(
        Func<DateTimeOffset>? clock = null,
        IIntruderSelfieService? selfies = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "dwarpal-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var fixture = new TestDatabase(root);
        await fixture.Database.InitializeAsync();

        fixture.Activity = new ActivityRepository(fixture.Database);
        fixture.Settings = new SettingsRepository(fixture.Database);
        fixture.Credentials = new CredentialRepository(fixture.Database, new DpapiProtector());
        fixture.Profiles = new ProfileRepository(fixture.Database, fixture.Settings);
        fixture.Schedules = new ScheduleRepository(fixture.Database);
        fixture.Lockout = new LockoutService(fixture.Settings, clock);
        fixture.CredentialService = new CredentialService(
            fixture.Credentials,
            new Argon2idHasher(memoryKb: 8192, iterations: 1, parallelism: 1),
            new ActivityLogger(fixture.Activity),
            fixture.Lockout,
            selfies);

        return fixture;
    }

    public async ValueTask DisposeAsync()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        await Task.Yield();

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Test cleanup best effort - temp folder pore clean kora jabe.
        }
    }
}
