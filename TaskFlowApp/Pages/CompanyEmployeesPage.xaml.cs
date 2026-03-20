using System.ComponentModel;
using TaskFlowApp.Models.Identity;
using TaskFlowApp.ViewModels;

namespace TaskFlowApp.Pages;

public partial class CompanyEmployeesPage : ContentPage
{
    private CompanyEmployeesPageViewModel ViewModel => (CompanyEmployeesPageViewModel)BindingContext;
    private bool isShowingFormMessage;
    private bool isViewModelSubscribed;

    public CompanyEmployeesPage(CompanyEmployeesPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        EnsureViewModelSubscription();
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        RemoveViewModelSubscription();
    }

    private async void OnDepartmentSelectTapped(object? sender, TappedEventArgs e)
    {
        var departments = await EnsureDepartmentsLoadedAsync();
        if (departments.Count == 0)
        {
            await DisplayAlertAsync("Bilgi", "Departman listesi şu anda alınamadı.", "Tamam");
            return;
        }

        var cancelText = "İptal";
        var selectedName = await DisplayActionSheetAsync(
            "Departman Seçin",
            cancelText,
            null,
            departments.Select(item => item.Name).ToArray());

        if (string.IsNullOrWhiteSpace(selectedName) || selectedName == cancelText)
        {
            return;
        }

        DepartmentDto? selectedDepartment = departments.FirstOrDefault(item =>
            string.Equals(item.Name, selectedName, StringComparison.Ordinal));

        if (selectedDepartment is not null)
        {
            ViewModel.SelectedDepartment = selectedDepartment;
        }
    }

    private async void OnTransferDepartmentSelectTapped(object? sender, TappedEventArgs e)
    {
        var departments = await EnsureDepartmentsLoadedAsync();
        if (departments.Count == 0)
        {
            await DisplayAlertAsync("Bilgi", "Departman listesi şu anda alınamadı.", "Tamam");
            return;
        }

        var cancelText = "İptal";
        var selectedName = await DisplayActionSheetAsync(
            "Transfer Departmanı Seçin",
            cancelText,
            null,
            departments.Select(item => item.Name).ToArray());

        if (string.IsNullOrWhiteSpace(selectedName) || selectedName == cancelText)
        {
            return;
        }

        var selectedDepartment = departments.FirstOrDefault(item =>
            string.Equals(item.Name, selectedName, StringComparison.Ordinal));

        if (selectedDepartment is not null)
        {
            ViewModel.SelectedTransferDepartment = selectedDepartment;
        }
    }

    private async Task<List<DepartmentDto>> EnsureDepartmentsLoadedAsync()
    {
        var departments = ViewModel.Departments.ToList();

        if (departments.Count > 0 || ViewModel.IsBusy)
        {
            return departments;
        }

        await ViewModel.LoadCommand.ExecuteAsync(null);
        return ViewModel.Departments.ToList();
    }

    private async void OnUserSelectTapped(object? sender, TappedEventArgs e)
    {
        var users = ViewModel.CompanyUsers.ToList();
        if (users.Count == 0)
        {
            await DisplayAlertAsync("Bilgi", "Çalışanlar listesi şu anda alınamadı.", "Tamam");
            return;
        }

        var cancelText = "İptal";
        var selectedName = await DisplayActionSheetAsync(
            "Kullanıcı Seçin",
            cancelText,
            null,
            users.Select(item => item.Name).ToArray());

        if (string.IsNullOrWhiteSpace(selectedName) || selectedName == cancelText)
        {
            return;
        }

        var selectedUser = users.FirstOrDefault(item =>
            string.Equals(item.Name, selectedName, StringComparison.Ordinal));

        if (selectedUser is not null)
        {
            ViewModel.SelectedUser = selectedUser;
        }
    }

    private async void OnDeleteUserSelectTapped(object? sender, TappedEventArgs e)
    {
        var users = ViewModel.CompanyUsers.ToList();
        if (users.Count == 0)
        {
            await DisplayAlertAsync("Bilgi", "Çalışanlar listesi şu anda alınamadı.", "Tamam");
            return;
        }

        var cancelText = "İptal";
        var selectedName = await DisplayActionSheetAsync(
            "Silinecek Çalışanı Seçin",
            cancelText,
            null,
            users.Select(item => item.Name).ToArray());

        if (string.IsNullOrWhiteSpace(selectedName) || selectedName == cancelText)
        {
            return;
        }

        var selectedUser = users.FirstOrDefault(item =>
            string.Equals(item.Name, selectedName, StringComparison.Ordinal));

        if (selectedUser is not null)
        {
            ViewModel.SelectedDeleteUser = selectedUser;
        }
    }

    private async void OnPasswordUserSelectTapped(object? sender, TappedEventArgs e)
    {
        var users = ViewModel.CompanyUsers.ToList();
        if (users.Count == 0)
        {
            await DisplayAlertAsync("Bilgi", "Çalışanlar listesi şu anda alınamadı.", "Tamam");
            return;
        }

        var cancelText = "İptal";
        var selectedName = await DisplayActionSheetAsync(
            "Şifresi Değişecek Çalışanı Seçin",
            cancelText,
            null,
            users.Select(item => item.Name).ToArray());

        if (string.IsNullOrWhiteSpace(selectedName) || selectedName == cancelText)
        {
            return;
        }

        var selectedUser = users.FirstOrDefault(item =>
            string.Equals(item.Name, selectedName, StringComparison.Ordinal));

        if (selectedUser is not null)
        {
            ViewModel.SelectedPasswordUser = selectedUser;
        }
    }

    private async void OnLeaderDepartmentSelectTapped(object? sender, TappedEventArgs e)
    {
        var departments = await EnsureDepartmentsLoadedAsync();
        if (departments.Count == 0)
        {
            await DisplayAlertAsync("Bilgi", "Departman listesi şu anda alınamadı.", "Tamam");
            return;
        }

        var cancelText = "İptal";
        var selectedName = await DisplayActionSheetAsync(
            "Departman Seçin",
            cancelText,
            null,
            departments.Select(item => item.Name).ToArray());

        if (string.IsNullOrWhiteSpace(selectedName) || selectedName == cancelText)
        {
            return;
        }

        var selectedDepartment = departments.FirstOrDefault(item =>
            string.Equals(item.Name, selectedName, StringComparison.Ordinal));

        if (selectedDepartment is not null)
        {
            ViewModel.SelectedLeaderDepartment = selectedDepartment;
        }
    }

    private async void OnLeaderUserSelectTapped(object? sender, TappedEventArgs e)
    {
        var users = ViewModel.CompanyUsers.ToList();
        if (users.Count == 0)
        {
            await DisplayAlertAsync("Bilgi", "Çalışanlar listesi şu anda alınamadı.", "Tamam");
            return;
        }

        var cancelText = "İptal";
        var selectedName = await DisplayActionSheetAsync(
            "Lider Yapılacak Çalışanı Seçin",
            cancelText,
            null,
            users.Select(item => item.Name).ToArray());

        if (string.IsNullOrWhiteSpace(selectedName) || selectedName == cancelText)
        {
            return;
        }

        var selectedUser = users.FirstOrDefault(item =>
            string.Equals(item.Name, selectedName, StringComparison.Ordinal));

        if (selectedUser is not null)
        {
            ViewModel.SelectedLeaderUser = selectedUser;
        }
    }

    private void EnsureViewModelSubscription()
    {
        if (isViewModelSubscribed)
        {
            return;
        }

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        isViewModelSubscribed = true;
    }

    private void RemoveViewModelSubscription()
    {
        if (!isViewModelSubscribed)
        {
            return;
        }

        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        isViewModelSubscribed = false;
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CompanyEmployeesPageViewModel.FormMessage) || isShowingFormMessage)
        {
            return;
        }

        var formMessage = ViewModel.FormMessage;
        if (string.IsNullOrWhiteSpace(formMessage))
        {
            return;
        }

        isShowingFormMessage = true;
        try
        {
            await DisplayAlertAsync("Bilgi", formMessage, "Tamam");
            ViewModel.FormMessage = string.Empty;
        }
        finally
        {
            isShowingFormMessage = false;
        }
    }

    private async void OnHomeTapped(object? sender, TappedEventArgs e) => await ViewModel.NavigateHomeCommand.ExecuteAsync(null);
    private async void OnReportsTapped(object? sender, TappedEventArgs e) => await ViewModel.NavigateReportsCommand.ExecuteAsync(null);
    private async void OnTasksTapped(object? sender, TappedEventArgs e) => await ViewModel.NavigateTasksCommand.ExecuteAsync(null);
    private async void OnMessagesTapped(object? sender, TappedEventArgs e) => await ViewModel.NavigateMessagesCommand.ExecuteAsync(null);
    private async void OnNotificationsTapped(object? sender, TappedEventArgs e) => await ViewModel.NavigateNotificationsCommand.ExecuteAsync(null);
    private async void OnLogoutTapped(object? sender, TappedEventArgs e) => await ViewModel.LogoutCommand.ExecuteAsync(null);
}
