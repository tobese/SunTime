using System;

namespace SunTime.Services;

public static class SeasonCalculator
{
    /// <summary>Sunrise and sunset for one solar calendar event at the current location.</summary>
    public record SeasonData(DateTime Sunrise, DateTime Sunset);

    /// <summary>
    /// Returns [spring equinox, summer solstice, autumn equinox, winter solstice] for
    /// the given year and location, using standard (non-DST) UTC offset.
    /// </summary>
    public static SeasonData[] Compute(int year, double latitude, double longitude, TimeZoneInfo tz)
    {
        double Y = (year - 2000.0) / 1000.0;

        // Jean Meeus "Astronomical Algorithms" Table 27.a — mean Julian Ephemeris Day
        double springJde = 2451623.80984 + Y * (365242.37404 + Y * ( 0.05169 + Y * (-0.00411 - Y * 0.00057)));
        double summerJde = 2451716.56767 + Y * (365241.62603 + Y * ( 0.00325 + Y * ( 0.00888 - Y * 0.00030)));
        double autumnJde = 2451810.21715 + Y * (365242.01767 + Y * (-0.11575 + Y * ( 0.00337 + Y * 0.00078)));
        double winterJde = 2451900.05952 + Y * (365242.74049 + Y * (-0.06223 + Y * (-0.00823 + Y * 0.00032)));

        return
        [
            Calc(springJde, latitude, longitude, tz),
            Calc(summerJde, latitude, longitude, tz),
            Calc(autumnJde, latitude, longitude, tz),
            Calc(winterJde, latitude, longitude, tz),
        ];
    }

    private static SeasonData Calc(double jde, double lat, double lon, TimeZoneInfo tz)
    {
        var date      = JdeToUtc(jde);
        var noon      = DateTime.SpecifyKind(date.Date.AddHours(12), DateTimeKind.Utc);
        var utcOffset = tz.GetUtcOffset(noon);
        var sun       = SolarCalculator.Calculate(lat, lon, noon, utcOffset);
        return new SeasonData(sun.Sunrise, sun.Sunset);
    }

    private static DateTime JdeToUtc(double jde)
    {
        double jd = jde + 0.5;
        int    Z  = (int)jd;
        double F  = jd - Z;

        int alpha = (int)((Z - 1867216.25) / 36524.25);
        int A = Z >= 2299161 ? Z + 1 + alpha - alpha / 4 : Z;
        int B = A + 1524;
        int C = (int)((B - 122.1) / 365.25);
        int D = (int)(365.25 * C);
        int E = (int)((B - D) / 30.6001);

        double dayF  = B - D - (int)(30.6001 * E) + F;
        int    day   = (int)dayF;
        int    month = E < 14 ? E - 1 : E - 13;
        int    yr    = month > 2 ? C - 4716 : C - 4715;

        var tod = TimeSpan.FromDays(dayF - day);
        return new DateTime(yr, month, day, tod.Hours, tod.Minutes, tod.Seconds, DateTimeKind.Utc);
    }
}
