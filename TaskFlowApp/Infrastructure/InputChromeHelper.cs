#if ANDROID
using Android.Content.Res;
using AndroidTextView = Android.Widget.TextView;
using AColor = Android.Graphics.Color;
#endif

#if IOS || MACCATALYST
using UIKit;
#endif

#if WINDOWS
using Microsoft.UI.Xaml.Controls;
using WinUIColors = Microsoft.UI.Colors;
using WinUISolidColorBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;
using WinUIThickness = Microsoft.UI.Xaml.Thickness;
#endif

namespace TaskFlowApp.Infrastructure;

public static class InputChromeHelper
{
    public static void RemoveNativeChrome(object? platformView)
    {
#if ANDROID
        if (platformView is AndroidTextView nativeTextView)
        {
            nativeTextView.BackgroundTintList = ColorStateList.ValueOf(AColor.Transparent);
            nativeTextView.Background?.SetTintList(ColorStateList.ValueOf(AColor.Transparent));
            nativeTextView.SetBackgroundColor(AColor.Transparent);
            return;
        }
#endif

#if IOS || MACCATALYST
        if (platformView is UIView nativeView)
        {
            nativeView.BackgroundColor = UIColor.Clear;
            nativeView.Layer.BorderWidth = 0;
            nativeView.Layer.BorderColor = UIColor.Clear.CGColor;

            if (nativeView is UITextField textField)
            {
                textField.BorderStyle = UITextBorderStyle.None;
            }

            return;
        }
#endif

#if WINDOWS
        if (platformView is Control nativeControl)
        {
            var transparentBrush = new WinUISolidColorBrush(WinUIColors.Transparent);
            var noThickness = new WinUIThickness(0);

            nativeControl.BorderThickness = noThickness;
            nativeControl.BorderBrush = transparentBrush;
            nativeControl.Background = transparentBrush;
            nativeControl.FocusVisualPrimaryBrush = transparentBrush;
            nativeControl.FocusVisualSecondaryBrush = transparentBrush;
            nativeControl.UseSystemFocusVisuals = false;

            TrySetWindowsResource(nativeControl, "TextControlBorderThemeThickness", noThickness);
            TrySetWindowsResource(nativeControl, "TextControlBorderThemeThicknessFocused", noThickness);
            TrySetWindowsResource(nativeControl, "TextControlBorderBrush", transparentBrush);
            TrySetWindowsResource(nativeControl, "TextControlBorderBrushFocused", transparentBrush);
            TrySetWindowsResource(nativeControl, "TextControlBorderBrushPointerOver", transparentBrush);
        }
#endif
    }

#if WINDOWS
    private static void TrySetWindowsResource(Control control, string key, object value)
    {
        try
        {
            control.Resources[key] = value;
        }
        catch
        {
            // Some WinUI resource keys can be immutable at runtime for specific controls.
        }
    }
#endif
}
