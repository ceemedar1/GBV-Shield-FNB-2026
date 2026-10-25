using System.Windows.Input;
namespace GBV_Shield { public static class Constants { public const int ShelterSearchRadius = 10000; } }
namespace Microsoft.Maui.Controls { public sealed class Command(Action action) : ICommand { public event EventHandler? CanExecuteChanged { add {} remove {} } public bool CanExecute(object? p) => true; public void Execute(object? p) => action(); } }
namespace Microsoft.Maui.ApplicationModel { public sealed class Launcher { public static Launcher Default { get; } = new(); public Task OpenAsync(Uri uri) => Task.CompletedTask; } }
namespace Microsoft.Maui.ApplicationModel.Communication { public sealed class PhoneDialer { public static PhoneDialer Default { get; } = new(); public bool IsSupported => true; public void Open(string phone) {} } }
namespace Microsoft.Maui.Devices.Sensors
{
    public sealed class Location(double lat, double lon) { public double Latitude => lat; public double Longitude => lon; }
    public sealed class Placemark { public string? Thoroughfare {get;set;} public string? SubLocality {get;set;} public string? Locality {get;set;} }
    public sealed class Geocoding
    {
        public static Geocoding Default {get;} = new();
        public Task<IEnumerable<Location>> GetLocationsAsync(string address) => Task.FromResult<IEnumerable<Location>>([]);
        public Task<IEnumerable<Placemark>> GetPlacemarksAsync(double lat, double lon) => Task.FromResult<IEnumerable<Placemark>>([new() { Thoroughfare="Test Street", Locality="Test City" }]);
    }
}
