using TaskFlowApp.Infrastructure;
using TaskFlowApp.ViewModels;

namespace TaskFlowApp.Pages;

public partial class MessagesPage : ContentPage
{
    private MessagesPageViewModel ViewModel => (MessagesPageViewModel)BindingContext;

    public MessagesPage()
    {
        InitializeComponent();
        BindingContext = ServiceLocator.GetRequiredService<MessagesPageViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ViewModel.RegisterRealtimeHandlers();
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        ViewModel.UnregisterRealtimeHandlers();
    }

    private void OnInputHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is VisualElement element)
        {
            InputChromeHelper.RemoveNativeChrome(element.Handler?.PlatformView);
        }
    }

    private void OnOwnMessagePointerEntered(object? sender, PointerEventArgs e) => SetDeleteVisibility(sender, true);
    private void OnOwnMessagePointerExited(object? sender, PointerEventArgs e) => SetDeleteVisibility(sender, false);

    private void OnOwnMessageTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject bindable &&
            bindable.BindingContext is ConversationMessageItem message &&
            message.IsOwnMessage &&
            !message.IsDeleting)
        {
            message.IsDeleteVisible = !message.IsDeleteVisible;
        }
    }

    private async void OnDeleteMessageTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not ConversationMessageItem message)
        {
            return;
        }

        var shouldDelete = await DisplayAlertAsync(
            "Mesaj\u0131 Sil",
            "Bu mesaj\u0131 silmek istiyor musunuz?",
            "Sil",
            "Vazge\u00e7");

        if (!shouldDelete)
        {
            return;
        }

        await ViewModel.DeleteMessageCommand.ExecuteAsync(message);
    }

    private static void SetDeleteVisibility(object? sender, bool isVisible)
    {
        if (sender is BindableObject bindable &&
            bindable.BindingContext is ConversationMessageItem message &&
            message.IsOwnMessage &&
            !message.IsDeleting)
        {
            message.IsDeleteVisible = isVisible;
        }
    }

    private async void OnHomeTapped(object? sender, TappedEventArgs e) => await ViewModel.NavigateHomeCommand.ExecuteAsync(null);
    private async void OnReportsTapped(object? sender, TappedEventArgs e) => await ViewModel.NavigateReportsCommand.ExecuteAsync(null);
    private async void OnTasksTapped(object? sender, TappedEventArgs e) => await ViewModel.NavigateTasksCommand.ExecuteAsync(null);
    private async void OnLeaderTasksTapped(object? sender, TappedEventArgs e) => await ViewModel.NavigateLeaderTasksCommand.ExecuteAsync(null);
    private async void OnMessagesTapped(object? sender, TappedEventArgs e) => await ViewModel.NavigateMessagesCommand.ExecuteAsync(null);
    private async void OnNotificationsTapped(object? sender, TappedEventArgs e) => await ViewModel.NavigateNotificationsCommand.ExecuteAsync(null);
    private async void OnLogoutTapped(object? sender, TappedEventArgs e) => await ViewModel.LogoutCommand.ExecuteAsync(null);
}
