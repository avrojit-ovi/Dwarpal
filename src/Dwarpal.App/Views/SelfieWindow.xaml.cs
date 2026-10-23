using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Dwarpal.App.Services;
using Serilog;

namespace Dwarpal.App.Views;

/// <summary>Activity log theke intruder selfie dekhar window - DPAPI decrypt kore local e show kore.</summary>
public partial class SelfieWindow : Window
{
    private readonly string _path;

    public SelfieWindow(string path, string eventText, string timestampText)
    {
        InitializeComponent();

        _path = path;
        EventText.Text = eventText;
        TimestampText.Text = timestampText;

        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UiAnimations.FadeIn(this);
        LoadImage();
    }

    private void LoadImage()
    {
        if (string.IsNullOrWhiteSpace(_path))
        {
            ShowError("Selfie file er path nei.");
            return;
        }

        try
        {
            var bytes = AppServices.Selfies.Load(_path);
            var image = new BitmapImage();

            using (var stream = new MemoryStream(bytes))
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
            }

            image.Freeze();
            SelfieImage.Source = image;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not open intruder selfie");
            ShowError("Selfie ta khola gelo na. File delete hoye thakte pare othoba onno machine er file.");
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnDragAreaMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
