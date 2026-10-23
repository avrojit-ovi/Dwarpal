using System.Windows;
using System.Windows.Media.Animation;

namespace Dwarpal.App.Services;

public static class UiAnimations
{
    /// <summary>Window kholar somoy chhoto fade-in, jate app ta abrupt na lage.</summary>
    public static void FadeIn(Window window, double durationMs = 220)
    {
        var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        window.BeginAnimation(UIElement.OpacityProperty, animation);
    }
}
