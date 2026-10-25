using Android.App;
using Android.Util;
using Firebase.Messaging;
using GBV_Shield.Services;

namespace GBV_Shield.Platforms.Android.Services
{
    [Service(
        Name = "com.gbvshield.app.MyFirebaseMessagingService", // 👈 important: matches AndroidManifest
        Exported = false
    )]
    [IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
    public class MyFirebaseMessagingService : FirebaseMessagingService
    {
        public override void OnMessageReceived(RemoteMessage message)
        {
            try
            {
                Log.Debug("FCM", $"Message received: {message.Data}");

                var title = message.GetNotification()?.Title ?? "GBV Shield";
                var body = message.GetNotification()?.Body ?? "New alert";

                var notificationService = MauiProgram.CurrentApp
                                                     .Services
                                                     .GetService(typeof(NotificationService)) as NotificationService;

                notificationService?.HandleIncomingNotification(title, body);
            }
            catch (System.Exception ex) { Log.Error("FCM", $"Message callback failed: {ex}"); }
        }

        [System.Obsolete("Required override for the Firebase messaging callback.")]
        public override void OnNewToken(string token)
        {
            try
            {
                Log.Debug("FCM", "A new device token was received.");

                var notificationService = MauiProgram.CurrentApp
                                                     .Services
                                                     .GetService(typeof(NotificationService)) as NotificationService;

                if (notificationService != null)
                    notificationService.SaveToken(token).Forget("FCM.SaveToken");
            }
            catch (System.Exception ex) { Log.Error("FCM", $"Token callback failed: {ex}"); }
        }
    }
}
