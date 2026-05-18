using System;

namespace SunTime.Services;

public static class MoonCalculator
{
    public record MoonData(
        double PhaseFraction,        // 0..1  (0=new, 0.25=first quarter, 0.5=full, 0.75=last quarter)
        double IlluminatedFraction,  // 0..1
        double HourOnDial            // 0..24 — position on the 24 h clock ring
    );

    // Known new moon: 2000-01-06 18:14 UTC
    private const double NewMoonJd     = 2451550.26;
    private const double SynodicPeriod = 29.530588853; // days

    public static MoonData Calculate(DateTime utcNow, double localHour)
    {
        double jd      = ToJulianDay(utcNow);
        double ageDays = ((jd - NewMoonJd) % SynodicPeriod + SynodicPeriod) % SynodicPeriod;
        double fraction    = ageDays / SynodicPeriod;
        double illuminated = (1.0 - Math.Cos(fraction * 2.0 * Math.PI)) / 2.0;

        // Moon falls behind the sun by one full 24 h revolution per synodic month.
        // localHour anchors the moon in the same coordinate system as the sun icon.
        double hourOnDial = (localHour + fraction * 24.0) % 24.0;

        return new MoonData(fraction, illuminated, hourOnDial);
    }

    private static double ToJulianDay(DateTime utc)
    {
        int    y = utc.Year;
        int    m = utc.Month;
        double d = utc.Day + utc.TimeOfDay.TotalDays;
        if (m <= 2) { y--; m += 12; }
        int A = y / 100;
        int B = 2 - A + A / 4;
        return (int)(365.25 * (y + 4716)) + (int)(30.6001 * (m + 1)) + d + B - 1524.5;
    }
}
