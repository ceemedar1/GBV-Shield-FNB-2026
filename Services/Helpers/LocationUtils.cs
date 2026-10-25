using System.Globalization;

namespace GBV_Shield.Services
{
    public static class LocationUtils
    {
        /// <summary>
        /// Force dot-decimal regardless of phone language/region.
        /// </summary>
        public static string ToInvariant(double value) =>
            value.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Format lat/lng pair for Google Maps.
        /// </summary>
        public static string FormatLatLng(double lat, double lng) =>
            $"{lat.ToString(CultureInfo.InvariantCulture)},{lng.ToString(CultureInfo.InvariantCulture)}";
    }
}
