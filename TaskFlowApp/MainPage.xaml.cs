namespace TaskFlowApp
{
    public partial class MainPage : ContentPage
    {


        public MainPage()
        {
            InitializeComponent();
        }
        public async void OnLoginButtonClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("///DashBoardPage");
        }

    }
}
