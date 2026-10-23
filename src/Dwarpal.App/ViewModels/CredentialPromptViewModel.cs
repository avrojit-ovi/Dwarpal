using CommunityToolkit.Mvvm.ComponentModel;
using Dwarpal.Core.Models;

namespace Dwarpal.App.ViewModels;

public partial class CredentialPromptViewModel : ObservableObject
{
    [ObservableProperty]
    private string _reason = "Ei kaj ta korte hole credential lagbe.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPin), nameof(IsPassword))]
    private CredentialKind _kind = CredentialKind.Pin;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy), nameof(CanAttempt))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public LockoutCountdownViewModel Lockout { get; } = new();

    public CredentialPromptViewModel()
    {
        Lockout.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(LockoutCountdownViewModel.IsLockedOut))
            {
                OnPropertyChanged(nameof(CanAttempt));
            }
        };
    }

    public bool IsPin => Kind == CredentialKind.Pin;
    public bool IsPassword => Kind == CredentialKind.Password;
    public bool IsNotBusy => !IsBusy;
    public bool CanAttempt => !IsBusy && Lockout.CanAttempt;

    public void ClearError() => ErrorMessage = null;
}
