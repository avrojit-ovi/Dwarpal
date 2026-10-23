using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dwarpal.App.Services;
using Dwarpal.Core.Data;
using Dwarpal.Core.Models;
using Dwarpal.Core.Security;
using Serilog;

namespace Dwarpal.App.ViewModels;

public enum SetupStep
{
    Welcome,
    Credential,
    Recovery,
    Done,
}

public partial class SetupViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWelcomeStep), nameof(IsCredentialStep), nameof(IsRecoveryStep), nameof(IsDoneStep))]
    private SetupStep _step = SetupStep.Welcome;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CredentialHint), nameof(KindLabel))]
    private CredentialKind _kind = CredentialKind.Pin;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _recoveryKey = string.Empty;

    [ObservableProperty]
    private bool _recoveryAcknowledged;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsWelcomeStep => Step == SetupStep.Welcome;
    public bool IsCredentialStep => Step == SetupStep.Credential;
    public bool IsRecoveryStep => Step == SetupStep.Recovery;
    public bool IsDoneStep => Step == SetupStep.Done;
    public bool IsNotBusy => !IsBusy;

    public string CredentialHint => Kind == CredentialKind.Pin
        ? "4 theke 8 digit. Keyboard ba number pad diye type koro."
        : "Minimum 8 character, ekta letter ar ekta number must.";

    public string KindLabel => Kind == CredentialKind.Pin ? "PIN" : "Password";

    [RelayCommand]
    private void StartSetup()
    {
        ErrorMessage = null;
        Step = SetupStep.Credential;
    }

    [RelayCommand]
    private void Back()
    {
        ErrorMessage = null;

        Step = Step switch
        {
            SetupStep.Credential => SetupStep.Welcome,
            SetupStep.Recovery => SetupStep.Credential,
            _ => Step,
        };
    }

    partial void OnKindChanged(CredentialKind value) => ErrorMessage = null;

    /// <summary>
    /// PasswordBox er value code-behind theke ashe (PasswordBox.Password bind kora jay na).
    /// </summary>
    public async Task<bool> SubmitCredentialAsync(string secret, string confirm)
    {
        if (IsBusy)
        {
            return false;
        }

        ErrorMessage = null;

        if (!string.Equals(secret, confirm, StringComparison.Ordinal))
        {
            ErrorMessage = "Dui bar same PIN/password dite hobe.";
            return false;
        }

        var validation = PasswordPolicy.Validate(secret, Kind);
        if (!validation.IsValid)
        {
            ErrorMessage = validation.Error;
            return false;
        }

        IsBusy = true;
        try
        {
            var result = await AppServices.Credentials.SetupAsync(Kind, secret);
            if (!result.Success || result.RecoveryKey is null)
            {
                ErrorMessage = result.Error ?? "Credential save korte parlam na.";
                return false;
            }

            RecoveryKey = result.RecoveryKey;
            Step = SetupStep.Recovery;
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Credential setup failed");
            ErrorMessage = "Credential save korte parlam na: " + ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> ContinueFromRecoveryAsync()
    {
        if (!RecoveryAcknowledged || IsBusy)
        {
            return false;
        }

        IsBusy = true;
        try
        {
            await AppServices.Settings.SetAsync(
                SettingKeys.RecoveryKeyAcknowledgedUtc,
                DateTimeOffset.UtcNow.ToString("o"));

            Step = SetupStep.Done;
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not persist recovery key acknowledgement");
            ErrorMessage = "Setup complete korte parlam na: " + ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
