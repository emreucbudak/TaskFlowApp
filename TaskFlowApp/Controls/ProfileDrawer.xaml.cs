using Microsoft.Maui.ApplicationModel;

namespace TaskFlowApp.Controls;

public partial class ProfileDrawer : ContentView
{
    private const double ClosedOffset = 320;

    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(
        nameof(IsOpen),
        typeof(bool),
        typeof(ProfileDrawer),
        false,
        propertyChanged: OnIsOpenChanged);

    public static readonly BindableProperty ActiveItemProperty = BindableProperty.Create(
        nameof(ActiveItem),
        typeof(string),
        typeof(ProfileDrawer),
        string.Empty,
        propertyChanged: OnActiveItemChanged);

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public string ActiveItem
    {
        get => (string)GetValue(ActiveItemProperty);
        set => SetValue(ActiveItemProperty, value);
    }

    public ProfileDrawer()
    {
        InitializeComponent();
        ApplyClosedState();
        ApplyActiveState();
    }

    private static void OnIsOpenChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var drawer = (ProfileDrawer)bindable;
        MainThread.BeginInvokeOnMainThread(async () => await drawer.UpdateDrawerStateAsync((bool)newValue));
    }

    private static void OnActiveItemChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((ProfileDrawer)bindable).ApplyActiveState();
    }

    private Task UpdateDrawerStateAsync(bool isOpen)
    {
        Backdrop.CancelAnimations();
        DrawerPanel.CancelAnimations();

        if (isOpen)
        {
            IsVisible = true;
            InputTransparent = false;
            Backdrop.Opacity = 0.38;
            DrawerPanel.TranslationX = 0;
            return Task.CompletedTask;
        }

        ApplyClosedState();
        return Task.CompletedTask;
    }

    private void ApplyClosedState()
    {
        IsVisible = false;
        InputTransparent = true;
        Backdrop.Opacity = 0;
        DrawerPanel.TranslationX = ClosedOffset;
    }

    private void ApplyActiveState()
    {
        var activeItem = ActiveItem?.Trim();
        SetMenuState(ProfileMenuCard, activeItem, "Profile");
        SetMenuState(GroupMenuCard, activeItem, "Group");
        SetMenuState(ReportsMenuCard, activeItem, "Reports");
        SetMenuState(TasksMenuCard, activeItem, "Tasks");
        SetMenuState(LeaderMenuCard, activeItem, "Leader");
    }

    private static void SetMenuState(Border border, string? activeItem, string itemKey)
    {
        var isActive = string.Equals(activeItem, itemKey, StringComparison.OrdinalIgnoreCase);
        border.BackgroundColor = Color.FromArgb("#F8F7FF");
        border.Stroke = new SolidColorBrush(Color.FromArgb("#00000000"));
    }
}
