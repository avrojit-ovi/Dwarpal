using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Dwarpal.Core.Models;

namespace Dwarpal.App.ViewModels;

public partial class LockScreenViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Initial))]
    private string _appName = string.Empty;

    [ObservableProperty]
    private string _appPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIcon))]
    private ImageSource? _appIcon;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPin), nameof(IsPassword))]
    private CredentialKind _kind = CredentialKind.Pin;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy), nameof(CanAttempt))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    public LockoutCountdownViewModel Lockout { get; } = new();

    public LockScreenViewModel()
    {
        Lockout.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(LockoutCountdownViewModel.IsLockedOut))
            {
                OnPropertyChanged(nameof(CanAttempt));
            }
        };
    }

    public bool HasIcon => AppIcon is not null;
    public bool IsPin => Kind == CredentialKind.Pin;
    public bool IsPassword => Kind == CredentialKind.Password;
    public bool IsNotBusy => !IsBusy;
    public bool CanAttempt => !IsBusy && Lockout.CanAttempt;

    public string Initial => string.IsNullOrEmpty(AppName)
        ? "?"
        : AppName.Trim()[0].ToString().ToUpperInvariant();

    public void ClearError() => ErrorMessage = null;
}
