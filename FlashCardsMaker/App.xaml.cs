using Microsoft.Extensions.DependencyInjection;

namespace FlashCardsMaker
{
    public partial class App : Application
    {
        private readonly AppShell _appShell;

        public App(IServiceProvider services)
        {
            InitializeComponent();
            _appShell = services.GetRequiredService<AppShell>();
            UserAppTheme = AppTheme.Dark;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(_appShell);
        }
    }
}