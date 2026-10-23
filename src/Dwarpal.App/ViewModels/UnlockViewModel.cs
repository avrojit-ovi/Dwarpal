using CommunityToolkit.Mvvm.ComponentModel;
using Dwarpal.App.Services;
using Dwarpal.Core.Models;
using Serilog;

namespace Dwarpal.App.ViewModels;

public partial class UnlockViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPin), nameof(IsPassword))]
    private CredentialKind _kind = CredentialKind.Pin;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy), nameof(CanAttempt))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public LockoutCountdownViewModel Lockout { get; } = new();

    public bool IsPin => Kind == CredentialKind.Pin;
    public bool IsPassword => Kind == CredentialKind.Password;
    public bool IsNotBusy => !IsBusy;
    public bool CanAttempt => !IsBusy && Lockout.CanAttempt;

    public event EventHandler? Unlocked;

    public UnlockViewModel()
    {
        Lockout.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(LockoutCountdownViewModel.IsLockedOut))
            {
                OnPropertyChanged(nameof(CanAttempt));
            }
        };
    }

    public async Task LoadAsync()
    {
        try
        {
            Kind = await AppServices.Credentials.GetKindAsync() ?? CredentialKind.Pin;
            await Lockout.RefreshAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not load credential kind");
            ErrorMessage = "Credential load korte parlam na.";
        }
    }

    public async Task<bool> TryUnlockAsync(string secret)
    {
        if (IsBusy || Lockout.IsLockedOut)
        {
            return false;
        }

        if (string.IsNullOrEmpty(secret))
        {
            ErrorMessage = Kind == CredentialKind.Pin ? "PIN ta dao." : "Password ta dao.";
            return false;
        }

        ErrorMessage = null;
        IsBusy = true;

        try
        {
            var result = await AppServices.Credentials.VerifyAsync(secret, "App unlock");
            if (!result.Success)
            {
                ErrorMessage = result.Error;

                if (result.LockoutSeconds > 0)
                {
                    Lockout.Start(result.LockoutSeconds);
                }

                return false;
            }

            Lockout.Clear();
            Unlocked?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unlock failed unexpectedly");
            ErrorMessage = "Unlock korte parlam na: " + ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ClearError() => ErrorMessage = null;
}
