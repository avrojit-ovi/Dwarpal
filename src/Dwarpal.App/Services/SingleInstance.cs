using System.Threading;
using Serilog;

namespace Dwarpal.App.Services;

/// <summary>
/// Ek session e ekta Dwarpal instance. Ditio bar EXE chalale purano instance er window show hoy.
/// Watchdog ei mutex dekhe siddhanto ney app already cholche kina.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\Dwarpal.Instance";
    private const string ActivateEventName = @"Local\Dwarpal.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activateEvent;
    private readonly Thread _listener;
    private readonly Action _onActivate;
    private volatile bool _running = true;

    private SingleInstance(Mutex mutex, EventWaitHandle activateEvent, Action onActivate)
    {
        _mutex = mutex;
        _activateEvent = activateEvent;
        _onActivate = onActivate;

        _listener = new Thread(Listen)
        {
            IsBackground = true,
            Name = "Dwarpal activation listener",
        };
        _listener.Start();
    }

    public static SingleInstance? TryAcquire(Action onActivate, out bool alreadyRunning)
    {
        alreadyRunning = false;
        var mutex = new Mutex(initiallyOwned: false, MutexName);
        var acquired = false;

        try
        {
            acquired = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            // Purano instance crash koreche - mutex ownership amra peyechi.
            acquired = true;
        }

        if (!acquired)
        {
            mutex.Dispose();
            alreadyRunning = true;
            return null;
        }

        var activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        return new SingleInstance(mutex, activateEvent, onActivate);
    }

    /// <summary>Already-running instance ke bolo window ta dekhao.</summary>
    public static void SignalExisting()
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(ActivateEventName, out var activateEvent))
            {
                using (activateEvent)
                {
                    activateEvent.Set();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not signal the running Dwarpal instance");
        }
    }

    /// <summary>Watchdog check: main instance mutex hold kore kina.</summary>
    public static bool IsMainInstanceRunning()
    {
        try
        {
            if (!Mutex.TryOpenExisting(MutexName, out var mutex))
            {
                return false;
            }

            using (mutex)
            {
                try
                {
                    if (mutex.WaitOne(0))
                    {
                        mutex.ReleaseMutex();
                        return false;
                    }

                    return true;
                }
                catch (AbandonedMutexException)
                {
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not inspect the Dwarpal instance mutex");
            return false;
        }
    }

    private void Listen()
    {
        while (_running)
        {
            try
            {
                if (_activateEvent.WaitOne(250))
                {
                    _onActivate();
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Single-instance listener failed");
            }
        }
    }

    public void Dispose()
    {
        if (!_running)
        {
            return;
        }

        _running = false;

        try
        {
            _activateEvent.Set();
            _listener.Join(TimeSpan.FromSeconds(1));
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Stopping activation listener failed");
        }

        _activateEvent.Dispose();

        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Amra owner na hole release fail kore - already released.
        }

        _mutex.Dispose();
    }
}
