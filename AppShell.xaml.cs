using Microsoft.Maui.Controls;

namespace GBV_Shield
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Auth routes
            Routing.RegisterRoute("LoginPage", typeof(Features.Auth.LoginPage));
            Routing.RegisterRoute("RegisterPage", typeof(Features.Auth.RegisterPage));

            // Feature routes
            Routing.RegisterRoute("ChatPage", typeof(Features.ChatHotline.ChatPage));
            Routing.RegisterRoute("SOSPage", typeof(Features.SOS.SOSPage));
            Routing.RegisterRoute("ResourcesPage", typeof(Features.Resources.ResourcesPage));
            Routing.RegisterRoute("ReportPage", typeof(Features.Reporting.ReportPage));
            Routing.RegisterRoute("SettingsPage", typeof(Features.Settings.SettingsPage));
            Routing.RegisterRoute("ShelterPage", typeof(Features.Shelters.ShelterPage));
            Routing.RegisterRoute("MainPage", typeof(Features.Home.MainPage));

            // ✅ New: Stories
            Routing.RegisterRoute("StoriesPage", typeof(Features.Stories.StoriesPage));
            Routing.RegisterRoute("AddStoryPage", typeof(Features.Stories.AddStoryPage));
        }
    }
}
