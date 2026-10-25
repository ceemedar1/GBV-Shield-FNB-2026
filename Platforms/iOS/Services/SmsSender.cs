using System;
using Microsoft.Maui.ApplicationModel; // ✅ replaces Essentials
using Microsoft.Maui.Controls;
using Foundation;
using UIKit;

[assembly: Dependency(typeof(GBV_Shield.Platforms.iOS.Services.SmsSender))]
namespace GBV_Shield.Platforms.iOS.Services
{
    using GBV_Shield.Services;

    public class SmsSender : ISmsSender
    {
        public void Send(string phoneNumber, string message, bool silent)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(message))
                    return;

                // iOS does not support silent SMS — always opens composer
                var uri = new Uri($"sms:{phoneNumber}?&body={Uri.EscapeDataString(message)}");
                Launcher.OpenAsync(uri).SafeFireAndForget(); // ✅ fixed reference
                System.Diagnostics.Debug.WriteLine($"[SmsSender.iOS] Composer opened for {phoneNumber}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SmsSender.iOS] Error: {ex}");
            }
        }
    }

    internal static class TaskExtensions
    {
        public static async void SafeFireAndForget(this System.Threading.Tasks.Task task)
        {
            try { await task; } catch { }
        }
    }
}
