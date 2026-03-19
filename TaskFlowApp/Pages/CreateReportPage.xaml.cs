using System.ComponentModel;
using TaskFlowApp.Infrastructure.Constants;
using TaskFlowApp.Models.Identity;
using TaskFlowApp.ViewModels;

namespace TaskFlowApp.Pages;

public partial class CreateReportPage : ContentPage
{
    private CreateReportPageViewModel ViewModel => (CreateReportPageViewModel)BindingContext;
    private bool isShowingFormMessage;
    private bool isViewModelSubscribed;

    public CreateReportPage(CreateReportPageViewModel viewModel)
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

    private async void OnTopicSelectTapped(object? sender, TappedEventArgs e)
    {
        var topics = ReportTopics.All.ToArray();
        var cancelText = "İptal";

        var selectedName = await DisplayActionSheet(
            "Rapor Konusu Seçin",
            cancelText,
            null,
            topics);

        if (string.IsNullOrWhiteSpace(selectedName) || selectedName == cancelText)
        {
            return;
        }

        var topicId = ReportTopics.GetId(selectedName);

        if (topicId > 0)
        {
            ViewModel.SelectedTopicId = topicId;
            ViewModel.SelectedTopicDisplayText = selectedName;
        }
    }

    private async void OnDepartmentSelectTapped(object? sender, TappedEventArgs e)
    {
        var departments = ViewModel.Departments.ToList();
        if (departments.Count == 0)
        {
            await DisplayAlert("Bilgi", "Departman listesi şu anda alınamadı.", "Tamam");
            return;
        }

        var cancelText = "İptal";
        var selectedName = await DisplayActionSheet(
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
            ViewModel.SelectedDepartment = selectedDepartment;
            ViewModel.SelectedDepartmentDisplayText = selectedDepartment.Name;
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
        if (e.PropertyName != nameof(CreateReportPageViewModel.FormMessage) || isShowingFormMessage)
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
            await DisplayAlert("Bilgi", formMessage, "Tamam");
            ViewModel.FormMessage = string.Empty;
        }
        finally
        {
            isShowingFormMessage = false;
        }
    }
}
