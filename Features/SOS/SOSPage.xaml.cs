using GBV_Shield.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace GBV_Shield.Features.SOS
{
    public partial class SOSPage : ContentPage
    {
        private readonly LocationService _locationService;

        public SOSPage()
        {
            InitializeComponent();
            _locationService = App.Services.GetRequiredService<LocationService>();
        }

        private async void OnSendSOSClicked(object sender, EventArgs e)
        {
            try { await SosService.SendSosAsync(silent: false); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SOS.Send] {ex}");
                try { await DisplayAlertAsync("SOS", "The SOS could not be sent. Please call 112 if you are in immediate danger.", "OK"); } catch { }
            }
        }

        private async void OnCallHotlineClicked(object sender, EventArgs e)
        {
            try
            {
                string hotlineNumber = Preferences.Get("HotlineNumber", "112");

                if (PhoneDialer.Default.IsSupported)
                    PhoneDialer.Default.Open(hotlineNumber);
                else
                    await Launcher.Default.OpenAsync(new Uri($"tel:{hotlineNumber}"));
            }
            catch (Exception ex)
            {
                try { await DisplayAlertAsync("Error", $"Could not place call: {ex.Message}", "OK"); } catch { }
            }
        }
    }
}
