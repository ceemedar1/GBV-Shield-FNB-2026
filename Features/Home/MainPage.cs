using Microsoft.Maui;
using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;
using System.Diagnostics;

namespace GBV_Shield.Features.Home
{
    public partial class MainPage : ContentPage
    {
        private bool _isNavigating;

        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnTileTapped(object sender, TappedEventArgs e)
        {
            if (_isNavigating || sender is not Border tile) return;
            _isNavigating = true;

            try
            {
                await tile.ScaleToAsync(0.96, 100, Easing.CubicOut);
                await tile.ScaleToAsync(1, 100, Easing.CubicIn);

                var param = e.Parameter as string;
                if (string.IsNullOrWhiteSpace(param)) return;

                await Shell.Current.GoToAsync(param);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Home.Navigation] {ex}");
                try { await DisplayAlertAsync("Navigation", "That page could not be opened. Please try again.", "OK"); }
                catch (Exception alertEx) { Debug.WriteLine($"[Home.Alert] {alertEx.Message}"); }
            }
            finally
            {
                tile.Scale = 1;
                _isNavigating = false;
            }
        }
    }
}
