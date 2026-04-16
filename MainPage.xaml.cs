using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SunTime.Services;

namespace SunTime;

public sealed partial class MainPage : Page
{
    private double _latitude;
    private double _longitude;
    private DispatcherTimer? _timer;

    public MainPage()
    {
        this.InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        DstToggle.IsOn = SettingsService.AdjustApexForDst;
        DstToggle.Toggled += DstToggle_Toggled;

        var (lat, lon, isFallback) = await LocationService.GetLocationAsync();
        _latitude = lat;
        _longitude = lon;

        StatusText.Text = isFallback
            ? $"Stockholm (fallback) — {lat:F2}°N, {lon:F2}°E"
            : $"{lat:F2}°N, {lon:F2}°E";

        Refresh();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
    }

    private void DstToggle_Toggled(object sender, RoutedEventArgs e)
    {
        SettingsService.AdjustApexForDst = DstToggle.IsOn;
        Refresh();
    }

    private void Refresh()
    {
        var utcNow = DateTime.UtcNow;

        var effectiveOffset = SettingsService.AdjustApexForDst
            ? TimeZoneInfo.Local.GetUtcOffset(utcNow)
            : TimeZoneInfo.Local.BaseUtcOffset;

        var sun = SolarCalculator.Calculate(_latitude, _longitude, utcNow, effectiveOffset);

        var utcYesterday = utcNow - TimeSpan.FromDays(1);
        var yesterdayOffset = SettingsService.AdjustApexForDst
            ? TimeZoneInfo.Local.GetUtcOffset(utcYesterday)
            : TimeZoneInfo.Local.BaseUtcOffset;
        var yesterdaySun = SolarCalculator.Calculate(_latitude, _longitude, utcYesterday, yesterdayOffset);

        var localNow = utcNow + effectiveOffset;
        Dial.Update(sun, localNow, yesterdaySun);
    }
}
