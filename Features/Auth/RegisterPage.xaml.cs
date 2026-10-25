using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Maui.Controls;
using GBV_Shield.Services;

namespace GBV_Shield.Features.Auth
{
    public partial class RegisterPage : ContentPage
    {
        private readonly AuthService _authService = new();
        private bool _busy;

        public RegisterPage()
        {
            InitializeComponent();
            EnsureXamlFieldsInitialized();
        }

        // ✅ Helps nullable analysis (ensures fields are set by XAML)
        [MemberNotNull(nameof(Busy),
                       nameof(FullNameEntry),
                       nameof(PhoneEntry),
                       nameof(EmailEntry),
                       nameof(PasswordEntry))]
        private void EnsureXamlFieldsInitialized()
        {
            if (Busy is null || FullNameEntry is null || PhoneEntry is null
                || EmailEntry is null || PasswordEntry is null)
            {
                throw new InvalidOperationException("XAML controls not initialized.");
            }
        }

        private async void OnRegisterClicked(object sender, EventArgs e)
        {
            if (_busy) return;
            _busy = true;
            Busy.IsVisible = Busy.IsRunning = true;

            string fullName = FullNameEntry.Text?.Trim() ?? string.Empty;
            string phone = PhoneEntry.Text?.Trim() ?? string.Empty;
            string email = EmailEntry.Text?.Trim() ?? string.Empty;
            string password = PasswordEntry.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                Busy.IsVisible = Busy.IsRunning = false;
                try { await DisplayAlertAsync("Error", "Full name, email and password are required.", "OK"); } catch { }
                _busy = false;
                return;
            }

            try
            {
                var user = await _authService.RegisterAsync(fullName, phone, email, password);

                Busy.IsVisible = Busy.IsRunning = false;

                if (user != null)
                {
                    await DisplayAlertAsync("Success", "✅ Account created successfully!", "OK");
                    ((App)Application.Current!).SetRootPage(new AppShell());
                }
                else
                {
                    await DisplayAlertAsync("Error", "❌ Registration failed. Try again.", "OK");
                }
            }
            catch (Exception ex)
            {
                try { await DisplayAlertAsync("Error", $"❌ Registration failed: {ex.Message}", "OK"); } catch { }
            }
            finally { Busy.IsVisible = Busy.IsRunning = false; _busy = false; }
        }

        private async void OnGoToLoginClicked(object sender, EventArgs e)
        {
            try { await Navigation.PushAsync(new LoginPage()); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Register.Navigation] {ex}"); }
        }
    }
}

