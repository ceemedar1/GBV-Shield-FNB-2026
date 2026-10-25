using Foundation;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using UIKit;
using UserNotifications;

namespace GBV_Shield
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
        {
            // Request iOS system notification permission
            UNUserNotificationCenter.Current.RequestAuthorization(
                UNAuthorizationOptions.Alert | UNAuthorizationOptions.Badge | UNAuthorizationOptions.Sound,
                (approved, err) => { });

            return base.FinishedLaunching(application, launchOptions);
        }
    }
}
