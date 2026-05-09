using System;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.Storage;

namespace SunTime.Services;

public static class LocationService
{
    // Stockholm hardcoded fallback (only used when no GPS fix AND no cached position)
    private const double FallbackLatitude  = 59.33;
    private const double FallbackLongitude = 18.07;

    // Android emulator default GPS position — Googleplex, Mountain View, CA.
    // Treat this as "no real fix" so the app falls back to Stockholm instead of
    // showing bizarre Swedish-clock times with California coordinates.
    private const double EmulatorLatitude  = 37.4219983;
    private const double EmulatorLongitude = -122.0840575;
    private const double EmulatorTolerance = 0.001;

    private static bool IsEmulatorDefault(double lat, double lon) =>
        Math.Abs(lat - EmulatorLatitude)  < EmulatorTolerance &&
        Math.Abs(lon - EmulatorLongitude) < EmulatorTolerance;

    private const string LatKey = "LastKnownLatitude";
    private const string LonKey = "LastKnownLongitude";

    // -------------------------------------------------------------------------
    // Persistence helpers
    // -------------------------------------------------------------------------

    private static void SaveLastKnown(double lat, double lon)
    {
        var s = ApplicationData.Current.LocalSettings;
        s.Values[LatKey] = lat;
        s.Values[LonKey] = lon;
    }

    private static bool TryLoadLastKnown(out double lat, out double lon)
    {
        var s = ApplicationData.Current.LocalSettings;
        if (s.Values.TryGetValue(LatKey, out var rawLat) &&
            s.Values.TryGetValue(LonKey, out var rawLon))
        {
            lat = (double)rawLat;
            lon = (double)rawLon;
            return true;
        }
        lat = lon = 0;
        return false;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns (lat, lon, isFallback).
    /// isFallback is true only when we fell back to the hardcoded Stockholm coords.
    /// A cached last-known position returns isFallback = false.
    /// </summary>
    public static async Task<(double Latitude, double Longitude, bool IsFallback)> GetLocationAsync()
    {
        try
        {
            var geolocator = new Geolocator { DesiredAccuracyInMeters = 1000 };

            var access = await Geolocator.RequestAccessAsync();
            if (access != GeolocationAccessStatus.Allowed)
                return CachedOrFallback();

            var position = await geolocator.GetGeopositionAsync(
                maximumAge:  TimeSpan.FromMinutes(5),
                timeout:     TimeSpan.FromSeconds(10));

            var coord = position.Coordinate.Point.Position;

            // Ignore the Android emulator's hardcoded default position (Googleplex, CA).
            if (IsEmulatorDefault(coord.Latitude, coord.Longitude))
                return CachedOrFallback();

            SaveLastKnown(coord.Latitude, coord.Longitude);
            return (coord.Latitude, coord.Longitude, false);
        }
        catch
        {
            return CachedOrFallback();
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static (double Latitude, double Longitude, bool IsFallback) CachedOrFallback()
    {
        if (TryLoadLastKnown(out var lat, out var lon))
            return (lat, lon, false);           // last known — real position

        return (FallbackLatitude, FallbackLongitude, true);   // nothing cached yet
    }
}
