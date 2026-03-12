using TaskFlowApp.Infrastructure;
using TaskFlowApp.ViewModels;

namespace TaskFlowApp.Pages;

public partial class AllIndividualTasksPage : ContentPage
{
    private AllIndividualTasksPageViewModel ViewModel => (AllIndividualTasksPageViewModel)BindingContext;

    public AllIndividualTasksPage()
    {
        InitializeComponent();
        BindingContext = ServiceLocator.GetRequiredService<AllIndividualTasksPageViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }
}
