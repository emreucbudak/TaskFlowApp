using TaskFlowApp.Infrastructure;
using TaskFlowApp.ViewModels;

#if ANDROID
using Android.Content.Res;
using Android.Widget;
using AColor = Android.Graphics.Color;
#endif

#if WINDOWS
using Microsoft.UI.Xaml.Controls;
using WinUIColors = Microsoft.UI.Colors;
using WinUISolidColorBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;
using WinUIThickness = Microsoft.UI.Xaml.Thickness;
#endif

namespace TaskFlowApp;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BindingContext = ServiceLocator.GetRequiredService<MainPageViewModel>();
    }

    private void OnEntryHandlerChanged(object? sender, EventArgs e)
    {
#if ANDROID
        if (sender is Entry entry && entry.Handler?.PlatformView is EditText nativeEntry)
        {
            nativeEntry.BackgroundTintList = ColorStateList.ValueOf(AColor.Transparent);
            nativeEntry.Background?.SetTintList(ColorStateList.ValueOf(AColor.Transparent));
            nativeEntry.SetBackgroundColor(AColor.Transparent);
        }
#endif

#if WINDOWS
        if (sender is Entry windowsEntry && windowsEntry.Handler?.PlatformView is Control nativeControl)
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
