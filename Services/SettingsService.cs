using Windows.Storage;

namespace SunTime.Services;

/// <summary>
/// Persisted user settings backed by ApplicationData.LocalSettings
/// (maps to localStorage on WASM via Uno).
/// </summary>
public static class SettingsService
{
    private const string AdjustApexKey = "AdjustApexForDst";

    public static bool AdjustApexForDst
    {
        get
        {
            var values = ApplicationData.Current.LocalSettings.Values;
            return values.TryGetValue(AdjustApexKey, out var v) ? (bool)v : true;
        }
        set => ApplicationData.Current.LocalSettings.Values[AdjustApexKey] = value;
    }
}
