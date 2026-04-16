using System;

namespace SunTime.Services;

/// <summary>
/// Self-contained NOAA solar position algorithm.
/// Reference: https://gml.noaa.gov/grad/solcalc/solareqns.PDF
/// </summary>
public static class SolarCalculator
{
    public record SunData(
        DateTime Sunrise,
        DateTime Sunset,
        DateTime SolarNoon,
        double Altitude,
        double HourAngle);

    public static SunData Calculate(double latitude, double longitude, DateTime utcNow, TimeSpan utcOffset)
    {
        double jd = ToJulianDay(utcNow);
        double T = (jd - 2451545.0) / 36525.0;

        // Geometric Mean Longitude of Sun (deg)
        double L0 = Mod(280.46646 + T * (36000.76983 + 0.0003032 * T), 360);

        // Geometric Mean Anomaly of Sun (deg)
        double M = 357.52911 + T * (35999.05029 - 0.0001537 * T);

        // Eccentricity of Earth's orbit
        double e = 0.016708634 - T * (0.000042037 + 0.0000001267 * T);

        // Equation of Center (deg)
        double Mrad = Rad(M);
        double C = Math.Sin(Mrad) * (1.914602 - T * (0.004817 + 0.000014 * T))
                 + Math.Sin(2 * Mrad) * (0.019993 - 0.000101 * T)
                 + Math.Sin(3 * Mrad) * 0.000289;

        // Sun True & Apparent Longitude
        double sunTrueLong = L0 + C;
        double omega = 125.04 - 1934.136 * T;
        double sunAppLong = sunTrueLong - 0.00569 - 0.00478 * Math.Sin(Rad(omega));

        // Obliquity of Ecliptic
        double obliq0 = 23.0 + (26.0 + (21.448 - T * (46.815 + T * (0.00059 - T * 0.001813))) / 60.0) / 60.0;
        double obliq = obliq0 + 0.00256 * Math.Cos(Rad(omega));

        // Sun Declination (deg)
        double declination = Deg(Math.Asin(Math.Sin(Rad(obliq)) * Math.Sin(Rad(sunAppLong))));

        // Equation of Time (minutes)
        double y = Math.Pow(Math.Tan(Rad(obliq / 2)), 2);
        double eqTime = 4.0 * Deg(
            y * Math.Sin(2 * Rad(L0))
            - 2.0 * e * Math.Sin(Mrad)
            + 4.0 * e * y * Math.Sin(Mrad) * Math.Cos(2 * Rad(L0))
            - 0.5 * y * y * Math.Sin(4 * Rad(L0))
            - 1.25 * e * e * Math.Sin(2 * Mrad));

        // Solar Noon (minutes from midnight, local standard time)
        double solarNoonMin = 720.0 - 4.0 * longitude - eqTime + utcOffset.TotalMinutes;

        // Hour angle at sunrise/sunset (deg)
        double latRad = Rad(latitude);
        double decRad = Rad(declination);
        double cosHA = Math.Cos(Rad(90.833)) / (Math.Cos(latRad) * Math.Cos(decRad))
                     - Math.Tan(latRad) * Math.Tan(decRad);
        cosHA = Math.Clamp(cosHA, -1.0, 1.0); // polar day/night guard
        double ha = Deg(Math.Acos(cosHA));

        double sunriseMin = solarNoonMin - ha * 4.0;
        double sunsetMin  = solarNoonMin + ha * 4.0;

        var midnight = (utcNow + utcOffset).Date;
        var sunrise   = midnight.AddMinutes(sunriseMin);
        var sunset    = midnight.AddMinutes(sunsetMin);
        var solarNoon = midnight.AddMinutes(solarNoonMin);

        // Current altitude & hour angle
        double utcMinutes = utcNow.TimeOfDay.TotalMinutes;
        double tst = Mod(utcMinutes + eqTime + 4.0 * longitude, 1440);
        double hourAngle = tst / 4.0 - 180.0;

        double sinAlt = Math.Sin(latRad) * Math.Sin(decRad)
                      + Math.Cos(latRad) * Math.Cos(decRad) * Math.Cos(Rad(hourAngle));
        double altitude = Deg(Math.Asin(Math.Clamp(sinAlt, -1.0, 1.0)));

        return new SunData(sunrise, sunset, solarNoon, altitude, hourAngle);
    }

    // ── helpers ──────────────────────────────────────────────

    private static double Rad(double deg) => deg * Math.PI / 180.0;
    private static double Deg(double rad) => rad * 180.0 / Math.PI;
    private static double Mod(double a, double b) { double r = a % b; return r < 0 ? r + b : r; }

    private static double ToJulianDay(DateTime utc)
    {
        int y = utc.Year;
        int m = utc.Month;
        double d = utc.Day + utc.TimeOfDay.TotalDays;

        if (m <= 2) { y--; m += 12; }

        int A = y / 100;
        int B = 2 - A + A / 4;

        return (int)(365.25 * (y + 4716)) + (int)(30.6001 * (m + 1)) + d + B - 1524.5;
    }
}
