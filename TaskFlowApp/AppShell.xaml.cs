using TaskFlowApp.Pages;

namespace TaskFlowApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(GroupDetailsPage), typeof(GroupDetailsPage));
        }
    }
}