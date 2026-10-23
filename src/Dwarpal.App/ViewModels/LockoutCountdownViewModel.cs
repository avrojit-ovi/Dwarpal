using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Dwarpal.App.Services;
using Serilog;

namespace Dwarpal.App.ViewModels;

/// <summary>
/// Lockout cooldown countdown. CredentialService already attempts block kore; eta shudhu
/// UI ke countdown dekhiye input disable kore, jate user bar bar try na kore.
/// </summary>
public partial class LockoutCountdownViewModel : ObservableObject
{
    private DispatcherTimer? _timer;
    private int _remainingSeconds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAttempt))]
    private bool _isLockedOut;

    [ObservableProperty]
    private string _text = string.Empty;

    public bool CanAttempt => !IsLockedOut;

    public event EventHandler? Finished;

    public async Task RefreshAsync()
    {
        try
        {
            var status = await AppServices.Lockout.GetStatusAsync();

            if (status.IsLockedOut)
            {
                Start(status.RemainingSeconds);
            }
            else
            {
                // Recovery reset / cooldown expiry DB state clear kore, but ei UI timer
                // purano countdown chalu rekhe dite pare. Eta na clear korle notun PIN
                // diyeo input disabled thake.
                Clear();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not read lockout status");
        }
    }

    public void Start(int seconds)
    {
        _remainingSeconds = Math.Max(1, seconds);
        IsLockedOut = true;
        Text = BuildText(_remainingSeconds);

        _timer ??= new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromSeconds(1),
        };

        if (!_timer.IsEnabled)
        {
            // Tick ekbar e subscribe koro; Clear/Start repeatedly korle duplicate
            // subscriptions ek second e multiple decrement korte pare.
            _timer.Tick -= OnTick;
            _timer.Tick += OnTick;
            _timer.Start();
        }
    }

    public void Clear()
    {
        _timer?.Stop();
        IsLockedOut = false;
        Text = string.Empty;
        _remainingSeconds = 0;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _remainingSeconds--;

        if (_remainingSeconds <= 0)
        {
            _timer?.Stop();
            IsLockedOut = false;
            Text = string.Empty;
            Finished?.Invoke(this, EventArgs.Empty);
            return;
        }

        Text = BuildText(_remainingSeconds);
    }

    private static string BuildText(int seconds) => $"Locked out · try again in {seconds}s";
}
