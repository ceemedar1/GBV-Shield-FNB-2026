using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage; // Preferences
using GBV_Shield.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Diagnostics;

namespace GBV_Shield.Features.Settings
{
    public partial class SettingsPage : ContentPage
    {
        private const string PrefHotline = "HotlineNumber";
        private const string PrefContacts = "EmergencyContacts";
        private const string PrefLogin = "IsLoggedIn";
        private bool _loadingSettings;

        public SettingsPage()
        {
            InitializeComponent();

            // Load saved values when page is opened
            _loadingSettings = true;
            try
            {
                HotlineEntry.Text = Preferences.Get(PrefHotline, "112");
                ContactsEditor.Text = Preferences.Get(PrefContacts, "");
                ShakeSosSwitch.IsToggled = ShakeDetectionService.IsEnabled;
                UpdateShakeStatus(ShakeDetectionService.IsEnabled);
                UpdateLiveLocationStatus();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings.Load] {ex}");
                HotlineEntry.Text = "112";
                ContactsEditor.Text = "";
            }
            finally { _loadingSettings = false; }
        }

        private void UpdateLiveLocationStatus()
        {
            var service = App.Services.GetService<LiveLocationService>();
            LiveLocationStatusLabel.Text = service?.IsActive == true
                ? "Live location is being shared for the active SOS session."
                : "No active live-location session";
        }

        private async void OnStopLiveLocationClicked(object sender, EventArgs e)
        {
            try
            {
                var service = App.Services.GetService<LiveLocationService>();
                if (service != null) await service.StopSessionAsync();
                UpdateLiveLocationStatus();
                await DisplayAlertAsync("Live location", "Live-location sharing has stopped.", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings.StopLocation] {ex}");
                try { await DisplayAlertAsync("Live location", "Could not stop sharing. Please try again.", "OK"); } catch { }
            }
        }

        private async void OnShakeSosToggled(object sender, ToggledEventArgs e)
        {
            if (_loadingSettings) return;

            try
            {

            if (e.Value && !await PermissionService.RequestShakeSosPermissionsAsync())
            {
                _loadingSettings = true;
                ShakeSosSwitch.IsToggled = false;
                _loadingSettings = false;
                ShakeDetectionService.SetEnabled(false);
                UpdateShakeStatus(false);
                await DisplayAlertAsync(
                    "Permissions required",
                    "Shake-to-SOS needs location access and, on Android, SMS permission.",
                    "OK");
                return;
            }

            var enabled = ShakeDetectionService.SetEnabled(e.Value);
            if (e.Value && !enabled)
            {
                _loadingSettings = true;
                ShakeSosSwitch.IsToggled = false;
                _loadingSettings = false;
                UpdateShakeStatus(false);
                await DisplayAlertAsync(
                    "Sensor unavailable",
                    "Shake-to-SOS could not start because the accelerometer is unavailable.",
                    "OK");
                return;
            }

                UpdateShakeStatus(enabled && e.Value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings.Shake] {ex}");
                _loadingSettings = true;
                ShakeSosSwitch.IsToggled = false;
                _loadingSettings = false;
                ShakeDetectionService.SetEnabled(false);
                UpdateShakeStatus(false);
                try { await DisplayAlertAsync("Shake SOS", "Shake protection could not be changed. Please try again.", "OK"); } catch { }
            }
        }

        private void UpdateShakeStatus(bool enabled)
        {
            ShakeStatusLabel.Text = enabled
                ? "Enabled — shake firmly three times within about two seconds."
                : "Disabled";
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
            var hotline = HotlineEntry.Text?.Trim();
            var contacts = ContactsEditor.Text?.Trim();

            // Save hotline
            Preferences.Set(PrefHotline, string.IsNullOrWhiteSpace(hotline) ? "112" : hotline);

            // Save contacts (limit to 10)
            if (!string.IsNullOrWhiteSpace(contacts))
            {
                var numbers = contacts.Split(',', StringSplitOptions.RemoveEmptyEntries);
                var top10 = string.Join(",", numbers.Take(10).Select(n => n.Trim()));
                Preferences.Set(PrefContacts, top10);
            }
            else
            {
                Preferences.Set(PrefContacts, "");
            }

                await DisplayAlertAsync("Saved", "✅ Your emergency settings have been saved.", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Settings.Save] {ex}");
                try { await DisplayAlertAsync("Not saved", "Your settings could not be saved. Please try again.", "OK"); } catch { }
            }
        }

        private void OnLogoutClicked(object sender, EventArgs e)
        {
            try
            {
                Preferences.Set(PrefLogin, false);
                App.Services.GetService<AuthService>()?.LogoutAsync().Forget("Settings.Logout");
                if (Application.Current is App app)
                    app.SetRootPage(new NavigationPage(new GBV_Shield.Features.Auth.LoginPage()));
            }
            catch (Exception ex) { Debug.WriteLine($"[Settings.Logout] {ex}"); }
        }
    }
}
