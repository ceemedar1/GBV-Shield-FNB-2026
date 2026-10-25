using System;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Maui.Controls;
using GBV_Shield.Services;
using Easing = Microsoft.Maui.Easing;

namespace GBV_Shield.Features.Common
{
    public partial class SplashPage : ContentPage
    {
        private readonly AuthService _auth;
        private readonly ReportsService _reports;
        private bool _startupStarted;

        public SplashPage(AuthService auth, ReportsService reports)
        {
            InitializeComponent();
            _auth = auth;
            _reports = reports;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (_startupStarted) return;
            _startupStarted = true;

            var start = DateTime.UtcNow;
            var workTask = DoStartupWorkAsync();

            try { await RunAnimationsAsync(); }
            catch (Exception ex) { Debug.WriteLine($"[Splash] Animation failed: {ex.Message}"); }

            var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
            if (elapsed < 1500)
            {
                try { await Task.Delay((int)(1500 - elapsed)); }
                catch (Exception ex) { Debug.WriteLine($"[Splash] Delay failed: {ex.Message}"); }
            }

            var loggedIn = false;
            try { loggedIn = await workTask; }
            catch (Exception ex) { Debug.WriteLine($"[Splash] Startup failed: {ex.Message}"); }

            try
            {
                if (Application.Current is App app)
                    app.SetRootPage(loggedIn ? new AppShell() : new NavigationPage(new Features.Auth.LoginPage()));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Splash] Navigation failed: {ex}");
                _startupStarted = false;
            }
        }

        private async Task RunAnimationsAsync()
        {
            if (LogoImage != null)
            {
                await LogoImage.FadeToAsync(1, 400, Easing.CubicOut);
                await LogoImage.ScaleToAsync(1.1, 400, Easing.SpringOut);
                await LogoImage.ScaleToAsync(1.0, 300, Easing.SpringIn);
            }

            if (LoadingLabel != null)
                await LoadingLabel.FadeToAsync(1, 600, Easing.CubicIn);
        }

        private async Task<bool> DoStartupWorkAsync()
        {
            try
            {
                await _reports.SyncPendingReportsAsync();
                return await _auth.IsLoggedInAsync();
            }
            catch
            {
                // if anything fails during startup, keep app usable
                return false;
            }
        }
    }
}
