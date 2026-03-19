using TaskFlowApp.ViewModels;

namespace TaskFlowApp.Pages;

public partial class AllIndividualTasksPage : ContentPage
{
    private AllIndividualTasksPageViewModel ViewModel => (AllIndividualTasksPageViewModel)BindingContext;

    public AllIndividualTasksPage(AllIndividualTasksPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }
}
