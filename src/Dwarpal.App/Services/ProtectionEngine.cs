using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using Dwarpal.App.Views;
using Dwarpal.Core.Data;
using Dwarpal.Core.Models;
using Dwarpal.Core.Monitoring;
using Dwarpal.Core.Processes;
using Dwarpal.Core.Security;
using Dwarpal.Core.Services;
using Serilog;

namespace Dwarpal.App.Services;

/// <summary>
/// App locker er engine:
/// WMI process-start event -> protected match -> suspend -> lock screen -> credential -> resume.
/// Ek protected app er sob process ek lock session e thake, tai Chrome/Discord er moto
/// multi-process app e ekta e lock screen ashe ar unlock e sob process resume hoy.
/// Sob UI operation engine er dispatcher e chole; WMI/WinEvent callback background theke ashe.
/// </summary>
public sealed class ProtectionEngine : IDisposable
{
    private readonly ProtectedAppRepository _repository;
    private readonly ProfileRepository _profiles;
    private readonly ScheduleRepository _schedules;
    private readonly CredentialService _credentials;
    private readonly IActivityLogger _activity;
    private readonly AppSettingsService _settings;
    private readonly Dispatcher _dispatcher;
    private readonly ProcessStartWatcher _watcher = new();
    private readonly ForegroundMonitor _foregroundMonitor = new();
    private readonly Dictionary<int, LockedSession> _lockedSessions = new();
    private readonly Dictionary<int, AutoRelockSession> _autoRelockSessions = new();
    private readonly DispatcherTimer _autoRelockTimer;
    private readonly DispatcherTimer _scheduleTimer;

    private IReadOnlyList<ProtectedApp> _apps = Array.Empty<ProtectedApp>();
    private IReadOnlyList<ProtectionSchedule> _scheduleWindows = Array.Empty<ProtectionSchedule>();
    private IReadOnlySet<int>? _activeAppIds;
    private AutoRelockSettings _autoRelockSettings = AutoRelockSettings.Default;
    private bool _wasWindowOpen = true;
    private bool _disposed;

    public ProtectionEngine(
        ProtectedAppRepository repository,
        ProfileRepository profiles,
        ScheduleRepository schedules,
        CredentialService credentials,
        IActivityLogger activity,
        AppSettingsService settings,
        Dispatcher dispatcher)
    {
        _repository = repository;
        _profiles = profiles;
        _schedules = schedules;
        _credentials = credentials;
        _activity = activity;
        _settings = settings;
        _dispatcher = dispatcher;

        _autoRelockTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _autoRelockTimer.Tick += OnAutoRelockTick;

        _scheduleTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(20),
        };
        _scheduleTimer.Tick += OnScheduleTick;
    }

    public bool IsRunning { get; private set; }
    public string? LastError { get; private set; }
    public int EnabledAppCount => _apps.Count(IsAppActive);
    public int LockedProcessCount => _lockedSessions.Values.Sum(session => session.ProcessIds.Count);

    public event EventHandler? StateChanged;

    public async Task StartAsync()
    {
        if (_disposed || IsRunning)
        {
            return;
        }

        await RefreshCacheAsync();
        _autoRelockSettings = await _settings.GetAutoRelockAsync();

        _watcher.ProcessStarted += OnProcessStarted;
        _foregroundMonitor.ForegroundProcessChanged += OnForegroundProcessChanged;
        _foregroundMonitor.MinimizeStarted += OnMinimizeStarted;

        try
        {
            _watcher.Start();
            IsRunning = true;
            LastError = null;
            Log.Information("Protection engine online with {Count} enabled apps", EnabledAppCount);
        }
        catch (Exception ex)
        {
            LastError = "Process watcher start korte parlam na: " + ex.Message;
            Log.Error(ex, "Could not start WMI process watcher");
            _watcher.ProcessStarted -= OnProcessStarted;
        }

        try
        {
            _foregroundMonitor.Start();
            _autoRelockTimer.Start();
            _wasWindowOpen = IsProtectionWindowOpen();
            _scheduleTimer.Start();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Foreground monitor start failed; auto re-lock disabled");
        }

        RaiseStateChanged();
    }

    /// <summary>Settings page theke call hoy - auto re-lock config reload.</summary>
    public async Task ReloadAutoRelockSettingsAsync()
    {
        _autoRelockSettings = await _settings.GetAutoRelockAsync();
    }

    /// <summary>
    /// Panic hotkey: schedule window ignore kore ekhoni sob running protected app lock kore.
    /// Return kore koto gulo notun app lock holo.
    /// </summary>
    public async Task<int> LockAllRunningAsync()
    {
        var locked = 0;

        foreach (var app in _apps.Where(IsAppActive))
        {
            var before = _lockedSessions.Count;
            await LockRunningInstancesAsync(app, force: true);
            locked += Math.Max(0, _lockedSessions.Count - before);
        }

        return locked;
    }

    /// <summary>
    /// Protected list change hole cache refresh + optionally already-running instance lock.
    /// </summary>
    public async Task RefreshAsync(bool lockRunningInstances)
    {
        await RefreshCacheAsync();
        _wasWindowOpen = IsProtectionWindowOpen();

        // Removed/disabled app er active auto-relock session ar dorkar nei.
        foreach (var session in _autoRelockSessions.Values.ToArray())
        {
            var fresh = _apps.FirstOrDefault(candidate => candidate.Id == session.App.Id);

            if (fresh is null || !IsAppActive(fresh))
            {
                _autoRelockSessions.Remove(session.App.Id);
            }
            else
            {
                session.App = fresh;
            }
        }

        // Locked session er app metadata refresh koro; lock screen ta user credential
        // chara bondho kora jabe na, tai removed app er session o unlock porjonto thake.
        foreach (var session in _lockedSessions.Values)
        {
            var fresh = _apps.FirstOrDefault(candidate => candidate.Id == session.App.Id);
            if (fresh is not null)
            {
                session.App = fresh;
            }
        }

        if (!lockRunningInstances)
        {
            return;
        }

        foreach (var app in _apps)
        {
            await LockRunningInstancesAsync(app, force: true);
        }
    }

    private async Task RefreshCacheAsync()
    {
        _apps = await _repository.GetAllAsync(enabledOnly: true);
        _activeAppIds = await _profiles.GetActiveAppIdsAsync();
        _scheduleWindows = await _schedules.GetAllAsync();
        RaiseStateChanged();
    }

    /// <summary>Schedule window bondho thakle notun app lock hobe na (panic hotkey e bypass hoy).</summary>
    private bool IsProtectionWindowOpen()
        => SchedulePolicy.IsProtectionActive(_scheduleWindows, DateTimeOffset.Now);

    /// <summary>Active profile null mane kono profile select kora nei - sob enabled app active.</summary>
    private bool IsAppActive(ProtectedApp app)
        => app.IsEnabled && (_activeAppIds is null || _activeAppIds.Contains(app.Id));

    private async Task LockRunningInstancesAsync(ProtectedApp app, bool force = false)
    {
        if (!IsAppActive(app))
        {
            return;
        }

        Process[] processes;

        try
        {
            processes = Process.GetProcessesByName(app.ProcessName);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not enumerate processes for {App}", app.DisplayName);
            return;
        }

        foreach (var process in processes)
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId)
                {
                    continue;
                }

                var path = ProcessController.TryGetExecutablePath(process.Id);
                if (ProtectedAppMatcher.Matches(app, process.ProcessName, path))
                {
                    await LockProcessAsync(app, process.Id, force);
                }
            }
        }
    }

    private void OnProcessStarted(object? sender, ProcessStartedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        // WMI thread theke UI thread e marshal kora hoy.
        _dispatcher.BeginInvoke(new Action(() => _ = HandleProcessStartAsync(e)), DispatcherPriority.Normal);
    }

    private async Task HandleProcessStartAsync(ProcessStartedEventArgs e)
    {
        try
        {
            if (_disposed || e.ProcessId == Environment.ProcessId)
            {
                return;
            }

            var processName = Path.GetFileNameWithoutExtension(e.ProcessName);

            // Nirdayi vabe nijer process ignore - lock screen er process o include.
            if (string.Equals(processName, "Dwarpal", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var app = _apps.FirstOrDefault(candidate =>
                IsAppActive(candidate)
                && string.Equals(candidate.ProcessName, processName, StringComparison.OrdinalIgnoreCase));

            if (app is null)
            {
                return;
            }

            var executablePath = ProcessController.TryGetExecutablePath(e.ProcessId);

            if (!ProtectedAppMatcher.Matches(app, e.ProcessName, executablePath))
            {
                return;
            }

            await LockProcessAsync(app, e.ProcessId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to handle process start for {ProcessName}", e.ProcessName);
        }
    }

    private async Task LockProcessAsync(ProtectedApp app, int processId, bool force = false)
    {
        if (_disposed || processId == Environment.ProcessId)
        {
            return;
        }

        // Same app er already ekta lock screen ache - notun child process ke oi
        // session e add koro, notun lock screen banio na.
        if (_lockedSessions.TryGetValue(app.Id, out var existing))
        {
            await AddProcessToSessionAsync(existing, app, processId);
            return;
        }

        // App unlocked obosthay cholche (auto re-lock session active). Chrome/Discord
        // er child process start hole take abar lock koro na; session e add koro.
        if (!force && _autoRelockSessions.TryGetValue(app.Id, out var autoSession))
        {
            autoSession.ProcessIds.RemoveWhere(candidate => !ProcessController.IsRunning(candidate));

            if (autoSession.ProcessIds.Count > 0)
            {
                autoSession.ProcessIds.Add(processId);
                return;
            }

            // Purano process gulo sob exit koreche - eta notun launch, lock hobe.
            _autoRelockSessions.Remove(app.Id);
        }

        if (!force && !IsProtectionWindowOpen())
        {
            return;
        }

        if (!ProcessController.IsRunning(processId))
        {
            return;
        }

        // Prothome suspend, tarpor UI - jate app tar content lock screen er niche na dekha jay.
        var suspended = ProcessController.TrySuspend(processId, out var suspendError);

        if (!suspended && !ProcessController.IsRunning(processId))
        {
            return;
        }

        var executablePath = ProcessController.TryGetExecutablePath(processId) ?? app.ExecutablePath;
        var icon = IconLoader.Load(executablePath, 96);

        LockScreenWindow window;

        try
        {
            window = new LockScreenWindow(new LockScreenContext(
                app,
                icon,
                secret => VerifySecretAsync(app, secret),
                () => IsAnyProcessRunning(app.Id)));
        }
        catch (Exception ex)
        {
            // Window construction fail korle process ke suspended chere dewa jabe na.
            Log.Error(ex, "Could not create lock screen for {App}", app.DisplayName);
            if (suspended)
            {
                ProcessController.TryResumeSilently(processId);
            }

            return;
        }

        var session = new LockedSession(app, processId);
        if (suspended)
        {
            session.SuspendedProcessIds.Add(processId);
        }

        session.Window = window;
        _lockedSessions[app.Id] = session;
        _autoRelockSessions.Remove(app.Id);

        window.Unlocked += async (_, _) =>
        {
            try
            {
                await CompleteSessionAsync(app.Id, resumeProcess: true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Completing unlock for {App} failed", app.DisplayName);
            }
        };

        window.ProcessExited += async (_, _) =>
        {
            try
            {
                await CompleteSessionAsync(app.Id, resumeProcess: false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Completing exited lock session for {App} failed", app.DisplayName);
            }
        };

        try
        {
            window.Show();
            window.Activate();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not show lock screen for {App}", app.DisplayName);
            _lockedSessions.Remove(app.Id);
            ProcessController.TryResumeSilently(processId);
            try
            {
                window.CloseForUnlock();
            }
            catch
            {
                // Best effort only - the session is already being abandoned.
            }

            return;
        }

        if (!suspended)
        {
            Log.Warning("Process {ProcessId} ({App}) could not be suspended: {Error}",
                processId, app.DisplayName, suspendError);
        }

        await _activity.LogAsync(
            ActivityEventType.Lock,
            suspended ? ActivityOutcome.Success : ActivityOutcome.Failure,
            suspended
                ? $"Suspended {app.DisplayName} and showed lock screen."
                : $"Lock screen shown, but suspend failed: {suspendError}",
            app.DisplayName,
            executablePath);

        await _repository.MarkLockedAsync(app.Id);
        RaiseStateChanged();
    }

    private async Task AddProcessToSessionAsync(LockedSession session, ProtectedApp app, int processId)
    {
        if (!ProcessController.IsRunning(processId) || !session.ProcessIds.Add(processId))
        {
            return;
        }

        var suspended = ProcessController.TrySuspend(processId, out var suspendError);
        if (suspended)
        {
            session.SuspendedProcessIds.Add(processId);
        }

        Log.Information(
            "Added process {ProcessId} to the existing {App} lock session (suspended={Suspended})",
            processId, app.DisplayName, suspended);

        await _activity.LogAsync(
            ActivityEventType.Lock,
            suspended ? ActivityOutcome.Success : ActivityOutcome.Failure,
            suspended
                ? $"Suspended another {app.DisplayName} process ({processId}) into the existing lock session."
                : $"Could not suspend another {app.DisplayName} process ({processId}): {suspendError}",
            app.DisplayName,
            app.ExecutablePath);

        RaiseStateChanged();
    }

    private bool IsAnyProcessRunning(int appId)
        => _lockedSessions.TryGetValue(appId, out var session)
           && session.ProcessIds.Any(ProcessController.IsRunning);

    private async Task<LockAttemptResult> VerifySecretAsync(ProtectedApp app, string secret)
    {
        var result = await _credentials.VerifyAsync(secret, $"Unlock {app.DisplayName}");
        return new LockAttemptResult(result.Success, result.Error, result.LockoutSeconds);
    }

    private async Task CompleteSessionAsync(int appId, bool resumeProcess)
    {
        if (!_lockedSessions.Remove(appId, out var session))
        {
            return;
        }

        var app = session.App;
        var appName = app.DisplayName;
        var appPath = app.ExecutablePath;
        var window = session.Window;

        // Lock screen ta sathe sathe bondho koro - activity log/resume e deri hole o
        // user er kache window atke thakbe na.
        try
        {
            window?.CloseForUnlock();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Lock window close after session completion failed");
        }

        if (resumeProcess)
        {
            var resumed = new List<int>();
            var failed = new List<string>();

            foreach (var processId in session.SuspendedProcessIds)
            {
                if (!ProcessController.IsRunning(processId))
                {
                    continue;
                }

                if (ProcessController.TryResume(processId, out var resumeError))
                {
                    resumed.Add(processId);
                }
                else
                {
                    failed.Add($"{processId}: {resumeError}");
                }
            }

            if (resumed.Count > 0)
            {
                await _activity.LogAsync(
                    ActivityEventType.Lock,
                    failed.Count == 0 ? ActivityOutcome.Success : ActivityOutcome.Failure,
                    failed.Count == 0
                        ? $"Resumed {appName} ({resumed.Count} process(es)) after unlock."
                        : $"Unlocked {appName}; resumed {resumed.Count} process(es), failed: {string.Join("; ", failed)}",
                    appName,
                    appPath);

                BeginAutoRelockSession(app, resumed);
            }
            else if (failed.Count > 0)
            {
                await _activity.LogAsync(
                    ActivityEventType.Lock,
                    ActivityOutcome.Failure,
                    $"Unlock succeeded but no {appName} process could be resumed: {string.Join("; ", failed)}",
                    appName,
                    appPath);
            }
            else
            {
                await _activity.LogAsync(
                    ActivityEventType.Lock,
                    ActivityOutcome.Info,
                    $"{appName} had no running suspended process to resume.",
                    appName,
                    appPath);
            }
        }
        else
        {
            _autoRelockSessions.Remove(appId);

            await _activity.LogAsync(
                ActivityEventType.Lock,
                ActivityOutcome.Info,
                $"{appName} exited while locked; nothing to resume.",
                appName,
                appPath);
        }

        RaiseStateChanged();
    }

    private void BeginAutoRelockSession(ProtectedApp app, IEnumerable<int> processIds)
    {
        _autoRelockSessions.Remove(app.Id);

        if (_autoRelockSettings.ResolveTimeout(app) is null)
        {
            return;
        }

        var running = processIds.Where(ProcessController.IsRunning).ToHashSet();
        if (running.Count == 0)
        {
            return;
        }

        // BackgroundedAt null: app foreground e thakle timeout count hobe na.
        _autoRelockSessions[app.Id] = new AutoRelockSession(app, running, DateTimeOffset.UtcNow);
    }

    private void OnForegroundProcessChanged(object? sender, int processId)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var session in _autoRelockSessions.Values)
        {
            if (session.ProcessIds.Contains(processId))
            {
                session.BackgroundedAt = null;
            }
            else
            {
                session.BackgroundedAt ??= now;
            }
        }
    }

    private void OnMinimizeStarted(object? sender, int processId)
    {
        if (_autoRelockSessions.Values.FirstOrDefault(session => session.ProcessIds.Contains(processId)) is { } session)
        {
            session.BackgroundedAt ??= DateTimeOffset.UtcNow;
        }
    }

    private async void OnAutoRelockTick(object? sender, EventArgs e)
    {
        if (_disposed || _autoRelockSessions.Count == 0)
        {
            return;
        }

        // Schedule window bondho - auto re-lock trigger hobe na.
        if (!IsProtectionWindowOpen())
        {
            return;
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            var idle = IdleDetector.GetIdleTime();
            var idleStartedAt = idle > TimeSpan.Zero ? now - idle : (DateTimeOffset?)null;

            foreach (var session in _autoRelockSessions.Values.ToArray())
            {
                session.ProcessIds.RemoveWhere(processId => !ProcessController.IsRunning(processId));

                if (session.ProcessIds.Count == 0)
                {
                    _autoRelockSessions.Remove(session.App.Id);
                    continue;
                }

                var timeout = _autoRelockSettings.ResolveTimeout(session.App);

                if (timeout is null)
                {
                    _autoRelockSessions.Remove(session.App.Id);
                    continue;
                }

                // App background e na thakleo, user idle thakle inactivity hisebe dhora hoy.
                var inactiveSince = session.BackgroundedAt;
                if (idleStartedAt is not null && (inactiveSince is null || idleStartedAt < inactiveSince))
                {
                    inactiveSince = idleStartedAt;
                }

                if (!AutoRelockPolicy.ShouldRelock(
                        now, session.UnlockedAt, inactiveSince, timeout, _autoRelockSettings.GraceSeconds))
                {
                    continue;
                }

                _autoRelockSessions.Remove(session.App.Id);

                await _activity.LogAsync(
                    ActivityEventType.Lock,
                    ActivityOutcome.Info,
                    $"Auto re-lock: {session.App.DisplayName} was inactive/background for {timeout} minute(s).",
                    session.App.DisplayName,
                    session.App.ExecutablePath);

                foreach (var processId in session.ProcessIds.ToArray())
                {
                    if (ProcessController.IsRunning(processId))
                    {
                        await LockProcessAsync(session.App, processId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Auto re-lock tick failed");
        }
    }

    /// <summary>
    /// Schedule boundary check: window khulle already-running protected app gulo lock koro.
    /// Window bondho hole already-locked screen gulo khola hoy na (safety), shudhu notun lock bondho.
    /// </summary>
    private async void OnScheduleTick(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var open = IsProtectionWindowOpen();

            if (open == _wasWindowOpen)
            {
                return;
            }

            _wasWindowOpen = open;

            if (open)
            {
                await _activity.LogAsync(
                    ActivityEventType.Lock,
                    ActivityOutcome.Info,
                    "Schedule window opened; locking running protected apps.");

                foreach (var app in _apps.Where(IsAppActive))
                {
                    await LockRunningInstancesAsync(app, force: true);
                }
            }
            else
            {
                await _activity.LogAsync(
                    ActivityEventType.Lock,
                    ActivityOutcome.Info,
                    "Schedule window closed; new protected app launches stay unlocked.");
            }

            RaiseStateChanged();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Schedule boundary tick failed");
        }
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _autoRelockTimer.Stop();
        _scheduleTimer.Stop();
        _scheduleTimer.Tick -= OnScheduleTick;
        _watcher.ProcessStarted -= OnProcessStarted;
        _foregroundMonitor.ForegroundProcessChanged -= OnForegroundProcessChanged;
        _foregroundMonitor.MinimizeStarted -= OnMinimizeStarted;
        _autoRelockSessions.Clear();

        try
        {
            _watcher.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Process watcher dispose failed");
        }

        try
        {
            _foregroundMonitor.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Foreground monitor dispose failed");
        }

        // App exit er somoy suspend kora process gulo stuck rakha jabe na - resume kore dey.
        foreach (var session in _lockedSessions.Values.ToArray())
        {
            foreach (var processId in session.SuspendedProcessIds)
            {
                ProcessController.TryResumeSilently(processId);
            }

            try
            {
                session.Window?.CloseForUnlock();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Lock window close during dispose failed");
            }
        }

        _lockedSessions.Clear();
        IsRunning = false;
        RaiseStateChanged();
    }

    private sealed class LockedSession
    {
        public LockedSession(ProtectedApp app, int processId)
        {
            App = app;
            ProcessIds = new HashSet<int> { processId };
        }

        public ProtectedApp App { get; set; }
        public HashSet<int> ProcessIds { get; }
        public HashSet<int> SuspendedProcessIds { get; } = new();
        public LockScreenWindow? Window { get; set; }
    }

    private sealed class AutoRelockSession
    {
        public AutoRelockSession(ProtectedApp app, HashSet<int> processIds, DateTimeOffset unlockedAt)
        {
            App = app;
            ProcessIds = processIds;
            UnlockedAt = unlockedAt;
        }

        public ProtectedApp App { get; set; }
        public HashSet<int> ProcessIds { get; }
        public DateTimeOffset UnlockedAt { get; }
        public DateTimeOffset? BackgroundedAt { get; set; }
    }
}
