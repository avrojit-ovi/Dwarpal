using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Dwarpal.App.Controls;

public partial class PinPad : UserControl
{
    public static readonly DependencyProperty SubmitTextProperty = DependencyProperty.Register(
        nameof(SubmitText),
        typeof(string),
        typeof(PinPad),
        new PropertyMetadata("Unlock"));

    public PinPad()
    {
        InitializeComponent();

        SecretBox.KeyDown += OnSecretKeyDown;
        Loaded += (_, _) => SecretBox.Focus();
    }

    public string SubmitText
    {
        get => (string)GetValue(SubmitTextProperty);
        set => SetValue(SubmitTextProperty, value);
    }

    /// <summary>Enter chaple lock window e submit hobe.</summary>
    public event EventHandler? Submitted;

    /// <summary>Prottek digit add/remove er por (error message clear korte useful).</summary>
    public event EventHandler? SecretChanged;

    public string Secret => SecretBox.Password;

    public void Clear()
    {
        SecretBox.Clear();
        SecretBox.Focus();
    }

    public void FocusInput() => SecretBox.Focus();

    private void OnSecretKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Submitted?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnDigitClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string digit })
        {
            return;
        }

        if (SecretBox.Password.Length >= SecretBox.MaxLength)
        {
            return;
        }

        SecretBox.Password += digit;
        SecretBox.Focus();
        SecretChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSubmitClick(object sender, RoutedEventArgs e)
    {
        Submitted?.Invoke(this, EventArgs.Empty);
        SecretBox.Focus();
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        SecretBox.Clear();
        SecretBox.Focus();
        SecretChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnBackspaceClick(object sender, RoutedEventArgs e)
    {
        if (SecretBox.Password.Length > 0)
        {
            SecretBox.Password = SecretBox.Password[..^1];
        }

        SecretBox.Focus();
        SecretChanged?.Invoke(this, EventArgs.Empty);
    }
}
