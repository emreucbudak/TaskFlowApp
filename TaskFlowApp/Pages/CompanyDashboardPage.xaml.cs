using System;
using System.Linq;
using TaskFlowApp.ViewModels;

namespace TaskFlowApp.Pages;

public partial class CompanyDashboardPage : ContentPage
{
    private CompanyDashboardPageViewModel ViewModel => (CompanyDashboardPageViewModel)BindingContext;

    public CompanyDashboardPage(CompanyDashboardPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void OnPeriodSelectTapped(object? sender, TappedEventArgs e)
    {
        var options = ViewModel.MonthOptions.ToList();
        if (options.Count == 0)
        {
            await DisplayAlertAsync("Bilgi", "Dönem listesi şu anda alınamadı.", "Tamam");
            return;
        }

        const string cancelText = "İptal";
        var selectedLabel = await DisplayActionSheetAsync(
            "Dönem Seçin",
            cancelText,
            null,
            options.Select(item => item.Label).ToArray());

        if (string.IsNullOrWhiteSpace(selectedLabel) || selectedLabel == cancelText)
        {
            return;
        }

        var selectedOption = options.FirstOrDefault(item =>
            string.Equals(item.Label, selectedLabel, StringComparison.Ordinal));

        if (selectedOption is not null)
        {
            ViewModel.SelectedMonthOption = selectedOption;
        }
    }
    private async void OnHomeTapped(object? sender, TappedEventArgs e) => await ViewModel.NavigateHomeCommand.ExecuteAsync(null);
    private async void OnReportsTapped(object? sender, TappedEventArgs e) => await ViewModel.NavigateReportsCommand.ExecuteAsync(null);
    private async void OnTasksTapped(object? sender, TappedEventArgs e) => await ViewModel.NavigateTasksCommand.ExecuteAsync(null);
    private async void OnMessagesTapped(object? sender, TappedEventArgs e) => await ViewModel.NavigateMessagesCommand.ExecuteAsync(null);
    private async void OnNotificationsTapped(object? sender, TappedEventArgs e) => await ViewModel.NavigateNotificationsCommand.ExecuteAsync(null);
    private async void OnLogoutTapped(object? sender, TappedEventArgs e) => await ViewModel.LogoutCommand.ExecuteAsync(null);
}

