using System.Windows;
using System.Windows.Threading;
using Dwarpal.Core;
using Dwarpal.Core.Applications;
using Dwarpal.Core.Data;
using Dwarpal.Core.Logging;
using Dwarpal.Core.Security;
using Dwarpal.Core.Services;
using Serilog;

namespace Dwarpal.App.Services;

/// <summary>
/// Composition root. WPF app choto, tai explicit static composition - DI container er dorkar nei.
/// InitializeAsync() startup e ekbar e call hoy.
/// </summary>
public static class AppServices
{
    public static AppPaths Paths { get; private set; } = null!;
    public static DwarpalDatabase Database { get; private set; } = null!;
    public static SettingsRepository Settings { get; private set; } = null!;
    public static ActivityRepository Activity { get; private set; } = null!;
    public static CredentialRepository CredentialRepository { get; private set; } = null!;
    public static ISecretHasher Hasher { get; private set; } = null!;
    public static CredentialService Credentials { get; private set; } = null!;
    public static LockoutService Lockout { get; private set; } = null!;
    public static SelfieStore Selfies { get; private set; } = null!;
    public static IIntruderSelfieService IntruderSelfies { get; private set; } = null!;
    public static ActivityLogger ActivityLog { get; private set; } = null!;
    public static ProtectedAppRepository ProtectedApps { get; private set; } = null!;
    public static ProfileRepository Profiles { get; private set; } = null!;
    public static ScheduleRepository Schedules { get; private set; } = null!;
    public static InstalledAppScanner InstalledApps { get; private set; } = null!;
    public static AppSettingsService AppSettings { get; private set; } = null!;
    public static ProtectionEngine? Protection { get; private set; }
    public static PanicHotkeyService PanicHotkey { get; private set; } = null!;

    /// <summary>App startup e DACL apply hoyeche kina (Settings page ekhane dekhabe).</summary>
    public static bool ProcessHardeningActive { get; set; }
    public static string? ProcessHardeningError { get; set; }

    public static async Task InitializeAsync(AppPaths? paths = null)
    {
        Paths = paths ?? new AppPaths();
        Paths.EnsureCreated();
        LoggingSetup.Initialize(Paths);

        Log.Information("Dwarpal starting. Data directory: {RootDirectory}", Paths.RootDirectory);

        Database = new DwarpalDatabase(Paths.DatabasePath);
        await Database.InitializeAsync();

        Settings = new SettingsRepository(Database);
        Activity = new ActivityRepository(Database);
        ActivityLog = new ActivityLogger(Activity);
        CredentialRepository = new CredentialRepository(Database, new DpapiProtector());
        Hasher = new Argon2idHasher();
        Lockout = new LockoutService(Settings);
        Selfies = new SelfieStore(Paths);
        AppSettings = new AppSettingsService(Settings);
        IntruderSelfies = new CameraSelfieService(AppSettings, Selfies);
        Credentials = new CredentialService(CredentialRepository, Hasher, ActivityLog, Lockout, IntruderSelfies);
        ProtectedApps = new ProtectedAppRepository(Database);
        Profiles = new ProfileRepository(Database, Settings);
        Schedules = new ScheduleRepository(Database);
        InstalledApps = new InstalledAppScanner();

        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        Protection = new ProtectionEngine(ProtectedApps, Profiles, Schedules, Credentials, ActivityLog, AppSettings, dispatcher);
        PanicHotkey = new PanicHotkeyService();
    }
}
