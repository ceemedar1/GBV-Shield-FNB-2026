using System;
using Android.Telephony;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

[assembly: Dependency(typeof(GBV_Shield.Platforms.Android.Services.SmsSender))]

namespace GBV_Shield.Platforms.Android.Services
{
    using GBV_Shield.Services;

    public class SmsSender : ISmsSender
    {
        public void Send(string phoneNumber, string message, bool silent)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(phoneNumber) ||
                    string.IsNullOrWhiteSpace(message))
                    return;

                if (silent)
                {
                    // ✅ Android-only silent SMS
#pragma warning disable CA1422
                    SmsManager.Default?
                        .SendTextMessage(phoneNumber, null, message, null, null);
#pragma warning restore CA1422

                    System.Diagnostics.Debug.WriteLine(
                        "[SmsSender.Android] Silent SMS sent to " + phoneNumber);
                }
                else
                {
                    var uri = new Uri(
                        "sms:" + phoneNumber + "?body=" +
                        Uri.EscapeDataString(message));

                    Launcher.OpenAsync(uri).SafeFireAndForget();

                    System.Diagnostics.Debug.WriteLine(
                        "[SmsSender.Android] Composer opened for " + phoneNumber);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[SmsSender.Android] Error: " + ex);
            }
        }
    }

    internal static class TaskExtensions
    {
        public static async void SafeFireAndForget(
            this System.Threading.Tasks.Task task)
        {
            try
            {
                await task;
            }
            catch
            {
                // intentionally ignored
            }
        }
    }
}