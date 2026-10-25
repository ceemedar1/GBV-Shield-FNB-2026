using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Extensions.DependencyInjection;

#if ANDROID
using Android.OS;
using Android.Telephony;
using Android.Content.PM;
using AndroidX.Core.Content;
#endif

using MauiSmsMessage = Microsoft.Maui.ApplicationModel.Communication.SmsMessage;

namespace GBV_Shield.Services
{
    public static class SosService
    {
        private const string PrefHotline = "HotlineNumber";
        private const string PrefContacts = "EmergencyContacts";

        private static Page? MainPage => Application.Current?.Windows.FirstOrDefault()?.Page;

        public static async Task<bool> SendSosAsync(bool silent = false)
        {
            try
            {
                if (!await EnsurePermissionsAsync(silent))
                    return false;

                var contactsString = Preferences.Get(PrefContacts, string.Empty);
                var contacts = contactsString
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim())
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .ToList();

                if (contacts.Count == 0)
                {
                    if (MainPage != null)
                        await MainPage.DisplayAlertAsync("SOS Failed", "No emergency contacts found in Settings.", "OK");
                    return false;
                }

                string addressPart = "(Location unavailable)";
                string mapsLink = "";
                Location? currentLocation = null;

                try
                {
                    var locationPermission = await Microsoft.Maui.ApplicationModel.Permissions.CheckStatusAsync<
                        Microsoft.Maui.ApplicationModel.Permissions.LocationWhenInUse>();
                    if (locationPermission != PermissionStatus.Granted && !silent)
                        locationPermission = await Microsoft.Maui.ApplicationModel.Permissions.RequestAsync<
                            Microsoft.Maui.ApplicationModel.Permissions.LocationWhenInUse>();

                    if (locationPermission == PermissionStatus.Granted)
                    {
                        var lastKnown = await Geolocation.Default.GetLastKnownLocationAsync();
                        currentLocation = lastKnown != null &&
                            DateTimeOffset.UtcNow - lastKnown.Timestamp <= TimeSpan.FromMinutes(2)
                                ? lastKnown
                                : await Geolocation.Default.GetLocationAsync(
                                    new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));
                    }

                    if (currentLocation != null)
                    {
                        addressPart = $"Lat: {currentLocation.Latitude:F5}, Lon: {currentLocation.Longitude:F5}";
                        mapsLink = $"https://www.google.com/maps/search/?api=1&query={currentLocation.Latitude},{currentLocation.Longitude}";
                    }
                }
                catch (Exception geEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[SOS] Geolocation failed: {geEx.Message}");
                }

                string liveTrackingLink = "";
                try
                {
                    var liveLocation = App.Services.GetService<LiveLocationService>();
                    if (liveLocation != null)
                        liveTrackingLink = await liveLocation.StartOrRefreshSessionAsync(currentLocation) ?? "";
                }
                catch (Exception liveEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[SOS] Live-location start failed: {liveEx.Message}");
                }

                var message =
                    "🚨 SOS ALERT 🚨\n" +
                    "I’m in danger, please help me urgently!\n\n" +
                    $"📍 {addressPart}\n" +
                    (string.IsNullOrEmpty(mapsLink) ? "" : $"🌍 Current location: {mapsLink}\n") +
                    (string.IsNullOrEmpty(liveTrackingLink) ? "" : $"🔴 Live tracking (8 hours): {liveTrackingLink}");

                var dispatched = false;
                foreach (var contact in contacts)
                {
                    try
                    {
#if ANDROID
                        // Both the visible SOS button and background shake must send
                        // through the app. MAUI's SMS composer is unsupported on some
                        // Huawei devices, and opening it does not actually send an SOS.
                        {
                            var context = Android.App.Application.Context;

                            if (context == null ||
                                ContextCompat.CheckSelfPermission(context, Android.Manifest.Permission.SendSms) != Permission.Granted)
                            {
                                System.Diagnostics.Debug.WriteLine("[SOS] SEND_SMS permission unavailable");
                                continue;
                            }

#pragma warning disable CA1422
                            var smsManager = SmsManager.Default;

                            if (smsManager != null)
                            {
                                var parts = smsManager.DivideMessage(message);
                                if (parts is { Count: > 1 })
                                    smsManager.SendMultipartTextMessage(contact, null, parts, null, null);
                                else
                                    smsManager.SendTextMessage(contact, null, message, null, null);
                                dispatched = true;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("[SOS] SmsManager unavailable");
                            }
#pragma warning restore CA1422
                            continue;
                        }
#else
                        var smsMessageNormal = new MauiSmsMessage(message, contact);
                        await Microsoft.Maui.ApplicationModel.Communication.Sms.ComposeAsync(smsMessageNormal);
                        dispatched = true;
#endif
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SOS] Failed sending to {contact}: {ex.Message}");
                    }
                }

                if (silent)
                {
                    try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(400)); } catch { }
                }
                else
                {
                    if (MainPage != null)
#if ANDROID
                        await MainPage.DisplayAlertAsync("SOS", dispatched
                            ? currentLocation != null
                                ? "SOS submitted to phone for sending with GPS location; delivery not confirmed."
                                : "SOS submitted to phone for sending, but GPS was unavailable; delivery not confirmed. Please share your location and call 112 if you are in danger."
                            : "SOS SMS could not be submitted. Please call 112 if you are in immediate danger.", "OK");
#else
                        await MainPage.DisplayAlertAsync("SOS", dispatched
                            ? "SMS composer opened. Please press Send. GBV Shield cannot confirm delivery."
                            : "No SMS composer opened. Please call 112 if you are in immediate danger.", "OK");
#endif
                }
                return dispatched;
            }
            catch (Exception ex)
            {
                if (MainPage != null)
                    await MainPage.DisplayAlertAsync("Error", $"SOS failed: {ex.Message}", "OK");
                return false;
            }
        }

        private static async Task<bool> EnsurePermissionsAsync(bool silent)
        {
#if ANDROID
            try
            {
                var context = Android.App.Application.Context;

                bool granted = context != null &&
                    ContextCompat.CheckSelfPermission(context, Android.Manifest.Permission.SendSms) == Permission.Granted;

                if (!granted && !silent)
                    granted = await Microsoft.Maui.ApplicationModel.Permissions.RequestAsync<
                        Microsoft.Maui.ApplicationModel.Permissions.Sms>() == PermissionStatus.Granted;

                if (!granted)
                {
                    if (!silent && MainPage != null)
                        await MainPage.DisplayAlertAsync(
                            "SMS Permission Needed",
                            "GBV Shield needs SMS permission to send your SOS. Please grant it in app settings or call 112.",
                            "OK");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EnsurePermissions] Android SMS check failed: {ex.Message}");
                return false;
            }
#endif
            return true;
        }

        public static async Task CallHotlineAsync()
        {
            try
            {
                var hotline = Preferences.Get(PrefHotline, string.Empty);
                if (string.IsNullOrWhiteSpace(hotline))
                {
                    if (MainPage != null)
                        await MainPage.DisplayAlertAsync("Hotline", "No hotline number is saved in settings.", "OK");
                    return;
                }

                var uri = new Uri($"tel:{hotline}");
                await Launcher.Default.OpenAsync(uri);
            }
            catch (Exception ex)
            {
                if (MainPage != null)
                    await MainPage.DisplayAlertAsync("Error", $"Unable to call hotline: {ex.Message}", "OK");
            }
        }
    }
}
