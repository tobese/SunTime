using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SunTime.Services;

namespace SunTime;

public sealed partial class MainPage : Page
{
    private double _latitude;
    private double _longitude;
    private DispatcherTimer? _timer;
#if DEBUG
    private DateTime? _debugUtcTime;
#endif

    public MainPage()
    {
        this.InitializeComponent();
        Loaded += OnLoaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        // Refresh the dial whenever we return from SettingsPage so DST changes take effect.
        if (_latitude != 0 || _longitude != 0)
            Refresh();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var (lat, lon, isFallback) = await LocationService.GetLocationAsync();
        _latitude = lat;
        _longitude = lon;

        Dial.LocationLabel = isFallback
            ? $"Stockholm (fallback)  {lat:F2}°N  {lon:F2}°E"
            : $"{lat:F2}°N  {lon:F2}°E";

        Refresh();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

#if DEBUG
        DebugBar.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        TimeSlider.Value = DateTime.Now.TimeOfDay.TotalMinutes;
        UpdateDebugTimeText();
        TimeSlider.ValueChanged += (_, _) =>
        {
            var localMidnight = DateTime.Today;
            var localDebug = localMidnight.AddMinutes(TimeSlider.Value);
            _debugUtcTime = localDebug.ToUniversalTime();
            UpdateDebugTimeText();
            Refresh();
        };
        DebugResetBtn.Click += (_, _) =>
        {
            _debugUtcTime = null;
            TimeSlider.Value = DateTime.Now.TimeOfDay.TotalMinutes;
            UpdateDebugTimeText();
            Refresh();
        };
#endif
    }

#if DEBUG
    private void UpdateDebugTimeText()
    {
        var minutes = (int)TimeSlider.Value;
        DebugTimeText.Text = $"{minutes / 60:D2}:{minutes % 60:D2}";
    }
#endif

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(SettingsPage));
    }

    private void Refresh()
    {
#if DEBUG
        var utcNow = _debugUtcTime ?? DateTime.UtcNow;
#else
        var utcNow = DateTime.UtcNow;
#endif

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
