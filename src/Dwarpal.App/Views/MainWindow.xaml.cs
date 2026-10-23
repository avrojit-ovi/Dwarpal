using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Dwarpal.App.Services;
using Dwarpal.App.ViewModels;
using Serilog;

namespace Dwarpal.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        Loaded += async (_, _) =>
        {
            UiAnimations.FadeIn(this);

            // Protection engine app unlock er por start hoy - phase 4 e service eta takeover korbe.
            try
            {
                if (AppServices.Protection is { IsRunning: false } protection)
                {
                    await protection.StartAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Protection engine start failed");
            }

            await _viewModel.LoadAsync();
        };
    }

    /// <summary>Credential-gated exit theke call hoy; normal close hide kore dey.</summary>
    public void AllowClose() => _allowClose = true;

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();

            if (Application.Current is App app)
            {
                app.NotifyMinimizedToTray();
            }
        }

        base.OnClosing(e);
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnDragAreaMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
