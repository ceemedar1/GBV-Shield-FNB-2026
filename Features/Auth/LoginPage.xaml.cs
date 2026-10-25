using System;
using Microsoft.Maui.Controls;
using GBV_Shield.Services;

namespace GBV_Shield.Features.Auth
{
    public partial class LoginPage : ContentPage
    {
        private readonly AuthService _authService = new();
        private bool _busy;

        public LoginPage()
        {
            InitializeComponent();
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            if (_busy) return;
            _busy = true;
            Busy.IsVisible = Busy.IsRunning = true;

            var email = EmailEntry?.Text?.Trim() ?? string.Empty;
            var password = PasswordEntry?.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                Busy.IsVisible = Busy.IsRunning = false;
                try { await DisplayAlertAsync("Error", "Please enter email & password", "OK"); } catch { }
                _busy = false;
                return;
            }

            try
            {
                var user = await _authService.LoginAsync(email, password);
                Busy.IsVisible = Busy.IsRunning = false;

                if (user != null)
                {
                    ((App)Application.Current!).SetRootPage(new AppShell());
                }
                else
                {
                    await DisplayAlertAsync("Error", "❌ Invalid credentials or network error.", "OK");
                }
            }
            catch (Exception ex)
            {
                try { await DisplayAlertAsync("Error", $"❌ Login failed: {ex.Message}", "OK"); } catch { }
            }
            finally { Busy.IsVisible = Busy.IsRunning = false; _busy = false; }
        }

        private async void OnGoToRegisterClicked(object sender, EventArgs e)
        {
            try { await Navigation.PushAsync(new RegisterPage()); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Login.Navigation] {ex}"); }
        }

        private async void OnQuickSendSOSClicked(object sender, EventArgs e)
        {
            try { await SosService.SendSosAsync(); }
            catch (Exception ex) { try { await DisplayAlertAsync("Error", $"Unable to send SOS: {ex.Message}", "OK"); } catch { } }
        }

        private async void OnQuickCallHotlineClicked(object sender, EventArgs e)
        {
            try { await SosService.CallHotlineAsync(); }
            catch (Exception ex) { try { await DisplayAlertAsync("Error", $"Unable to call hotline: {ex.Message}", "OK"); } catch { } }
        }
    }
}
