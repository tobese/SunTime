using System;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;

namespace SunTime.Services;

public static class LocationService
{
    // Stockholm fallback
    private const double FallbackLatitude  = 59.33;
    private const double FallbackLongitude = 18.07;

    public static async Task<(double Latitude, double Longitude, bool IsFallback)> GetLocationAsync()
    {
        try
        {
            var geolocator = new Geolocator
            {
                DesiredAccuracyInMeters = 1000
            };

            var access = await Geolocator.RequestAccessAsync();
            if (access != GeolocationAccessStatus.Allowed)
                return (FallbackLatitude, FallbackLongitude, true);

            var position = await geolocator.GetGeopositionAsync();
            var coord = position.Coordinate.Point.Position;
            return (coord.Latitude, coord.Longitude, false);
        }
        catch
        {
            return (FallbackLatitude, FallbackLongitude, true);
        }
    }
}
