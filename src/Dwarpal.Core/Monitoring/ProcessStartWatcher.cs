using System.Management;

namespace Dwarpal.Core.Monitoring;

public sealed record ProcessStartedEventArgs(int ProcessId, string ProcessName);

/// <summary>
/// WMI Win32_ProcessStartTrace diye notun process detect. Ei event er jonno admin lagbe
/// (Dwarpal always elevated chole), ar event arrive korte milliseconds lage.
/// </summary>
public sealed class ProcessStartWatcher : IDisposable
{
    private readonly object _gate = new();
    private ManagementEventWatcher? _watcher;
    private bool _disposed;

    public event EventHandler<ProcessStartedEventArgs>? ProcessStarted;

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _watcher is not null;
            }
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_watcher is not null)
            {
                return;
            }

            var scope = new ManagementScope(@"\\.\root\CIMV2");
            var query = new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace");
            var watcher = new ManagementEventWatcher(scope, query);

            watcher.EventArrived += OnEventArrived;
            watcher.Start();
            _watcher = watcher;
        }
    }

    public void Stop()
    {
        ManagementEventWatcher? watcher;

        lock (_gate)
        {
            watcher = _watcher;
            _watcher = null;
        }

        if (watcher is null)
        {
            return;
        }

        watcher.EventArrived -= OnEventArrived;

        try
        {
            watcher.Stop();
        }
        finally
        {
            watcher.Dispose();
        }
    }

    private void OnEventArrived(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var name = e.NewEvent.Properties["ProcessName"]?.Value as string;
            var idValue = e.NewEvent.Properties["ProcessID"]?.Value;

            if (string.IsNullOrWhiteSpace(name) || idValue is null)
            {
                return;
            }

            var processId = Convert.ToInt32(idValue);
            ProcessStarted?.Invoke(this, new ProcessStartedEventArgs(processId, name));
        }
        catch (Exception)
        {
            // WMI event payload corrupt hole baki watching continue korbe.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }
}
