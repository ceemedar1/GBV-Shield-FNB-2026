using System;
using System.Threading.Tasks;
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using Microsoft.Maui.Storage;

namespace GBV_Shield.Services
{
    public class NotificationService
    {
        private const string TokenKey = "device_fcm_token";

        /// <summary>
        /// Save the FCM device token securely.
        /// </summary>
        public async Task SaveToken(string token)
        {
            await SecureStorage.SetAsync(TokenKey, token);
            Console.WriteLine("✅ [NotificationService] Device token saved.");
        }

        /// <summary>
        /// Retrieve the saved device token (or empty if none).
        /// </summary>
        public async Task<string> GetDeviceTokenAsync()
        {
            return await SecureStorage.GetAsync(TokenKey) ?? string.Empty;
        }

        /// <summary>
        /// Handle push notifications (from Firebase).
        /// </summary>
        public void HandleIncomingNotification(string title, string body)
        {
            Console.WriteLine($"🔔 Push received: {title} - {body}");

            var request = new NotificationRequest
            {
                NotificationId = new Random().Next(1000, 9999),
                Title = title,
                Description = body,
                ReturningData = "PushPayload"
            };

            LocalNotificationCenter.Current.Show(request);
        }
    }
}
