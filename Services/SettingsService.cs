using Windows.Storage;

namespace SunTime.Services;

/// <summary>
/// Persisted user settings backed by ApplicationData.LocalSettings
/// (maps to localStorage on WASM via Uno).
/// </summary>
public static class SettingsService
{
    private const string AdjustApexKey   = "AdjustApexForDst";
    private const string ShowDurationsKey = "ShowDurations";
    private const string ShowApexTimeKey  = "ShowApexTime";
    private const string ShowWeekDiffsKey = "ShowWeekDiffs";
    private const string ShowSunAngleKey  = "ShowSunAngle";
    private const string NoonAtTopKey     = "NoonAtTop";

    /// <summary>Shift solar noon to match the clock during DST.</summary>
    public static bool AdjustApexForDst
    {
        get => GetBool(AdjustApexKey, defaultValue: true);
        set => SetBool(AdjustApexKey, value);
    }

    /// <summary>Show the daylight / nighttime duration labels inside the ring.</summary>
    public static bool ShowDurations
    {
        get => GetBool(ShowDurationsKey, defaultValue: false);
        set => SetBool(ShowDurationsKey, value);
    }

    /// <summary>Show the solar noon (apex) marker and its time label.</summary>
    public static bool ShowApexTime
    {
        get => GetBool(ShowApexTimeKey, defaultValue: false);
        set => SetBool(ShowApexTimeKey, value);
    }

    /// <summary>Show the day-over-day diff sectors and ±m/wk annotations.</summary>
    public static bool ShowWeekDiffs
    {
        get => GetBool(ShowWeekDiffsKey, defaultValue: false);
        set => SetBool(ShowWeekDiffsKey, value);
    }

    /// <summary>Show the current solar elevation angle near the sun icon.</summary>
    public static bool ShowSunAngle
    {
        get => GetBool(ShowSunAngleKey, defaultValue: false);
        set => SetBool(ShowSunAngleKey, value);
    }

    /// <summary>Rotate the clock so solar noon always appears at the 12-o'clock position.</summary>
    public static bool NoonAtTop
    {
        get => GetBool(NoonAtTopKey, defaultValue: false);
        set => SetBool(NoonAtTopKey, value);
    }

    // ── helpers ──────────────────────────────────────────────

    private static bool GetBool(string key, bool defaultValue)
    {
        var values = ApplicationData.Current.LocalSettings.Values;
        return values.TryGetValue(key, out var v) ? (bool)v : defaultValue;
    }

    private static void SetBool(string key, bool value) =>
        ApplicationData.Current.LocalSettings.Values[key] = value;
}
