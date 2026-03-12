namespace TaskFlowApp.Controls;

public partial class AppBottomNav : ContentView
{
    public static readonly BindableProperty ActiveItemProperty = BindableProperty.Create(
        nameof(ActiveItem),
        typeof(string),
        typeof(AppBottomNav),
        string.Empty,
        propertyChanged: OnActiveItemChanged);

    public string ActiveItem
    {
        get => (string)GetValue(ActiveItemProperty);
        set => SetValue(ActiveItemProperty, value);
    }

    public AppBottomNav()
    {
        InitializeComponent();
        ApplyActiveState();
    }

    private static void OnActiveItemChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((AppBottomNav)bindable).ApplyActiveState();
    }

    private void ApplyActiveState()
    {
        var activeItem = ActiveItem?.Trim();
        SetItemState(HomeBorder, HomeDot, activeItem, "Home");
        SetItemState(NotificationWorkerBorder, NotificationWorkerDot, activeItem, "Notifications");
        SetItemState(NotificationCompanyBorder, NotificationCompanyDot, activeItem, "Notifications");
        SetItemState(SecondaryWorkerBorder, SecondaryWorkerDot, activeItem, "Secondary");
        SetItemState(SecondaryCompanyBorder, SecondaryCompanyDot, activeItem, "Secondary");
        SetItemState(ProfileBorder, ProfileDot, activeItem, "Profile");
    }

    private static void SetItemState(Border border, BoxView dot, string? activeItem, string itemKey)
    {
        var isActive = string.Equals(activeItem, itemKey, StringComparison.OrdinalIgnoreCase);
        border.BackgroundColor = Color.FromArgb(isActive ? "#EEF2FF" : "#F8F7FF");
        border.Stroke = new SolidColorBrush(Color.FromArgb(isActive ? "#C7D2FE" : "#00000000"));
        dot.IsVisible = isActive;
    }
}
