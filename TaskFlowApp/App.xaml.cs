#if WINDOWS
using TaskFlowApp.Infrastructure.Windows;
using TaskFlowApp.Infrastructure.Payments;
#endif

namespace TaskFlowApp
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            UserAppTheme = AppTheme.Dark;
#if WINDOWS
            DesktopProtocolRegistrar.EnsureRegistered();
            PaymentReturnState.TryStoreFromCommandLine(Environment.GetCommandLineArgs());
#endif
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}

