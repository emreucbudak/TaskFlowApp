using TaskFlowApp.Pages;

namespace TaskFlowApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            ConfigurePresentationModes();
            Routing.RegisterRoute(nameof(GroupDetailsPage), typeof(GroupDetailsPage));
            Routing.RegisterRoute(nameof(ProfilePage), typeof(ProfilePage));
        }

        private void ConfigurePresentationModes()
        {
            Shell.SetPresentationMode(this, PresentationMode.NotAnimated);

            foreach (var item in Items)
            {
                Shell.SetPresentationMode(item, PresentationMode.NotAnimated);

                foreach (var section in item.Items)
                {
                    Shell.SetPresentationMode(section, PresentationMode.NotAnimated);

                    foreach (var content in section.Items)
                    {
                        Shell.SetPresentationMode(content, PresentationMode.NotAnimated);
                    }
                }
            }
        }
    }
}
