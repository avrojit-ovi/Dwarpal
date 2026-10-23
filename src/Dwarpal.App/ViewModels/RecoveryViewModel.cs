using CommunityToolkit.Mvvm.ComponentModel;
using Dwarpal.App.Services;
using Dwarpal.Core.Models;
using Dwarpal.Core.Security;
using Serilog;

namespace Dwarpal.App.ViewModels;

public partial class RecoveryViewModel : ObservableObject
{
    [ObservableProperty]
    private string _recoveryKeyInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CredentialHint))]
    private CredentialKind _kind = CredentialKind.Pin;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsForm), nameof(IsResult))]
    private string? _newRecoveryKey;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsForm => NewRecoveryKey is null;
    public bool IsResult => NewRecoveryKey is not null;
    public bool IsNotBusy => !IsBusy;

    public string CredentialHint => Kind == CredentialKind.Pin
        ? "Notun PIN 4-8 digit."
        : "Minimum 8 character, letter + number.";

    partial void OnKindChanged(CredentialKind value) => ErrorMessage = null;

    public async Task<bool> TryResetAsync(string secret, string confirm)
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
            var result = await AppServices.Credentials.ResetWithRecoveryKeyAsync(
                RecoveryKeyInput, Kind, secret);

            if (!result.Success || result.NewRecoveryKey is null)
            {
                ErrorMessage = result.Error ?? "Recovery fail hoyeche.";
                return false;
            }

            NewRecoveryKey = result.NewRecoveryKey;
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Recovery reset failed");
            ErrorMessage = "Recovery korte parlam na: " + ex.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
