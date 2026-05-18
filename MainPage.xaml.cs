using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SunTime.Services;
using Windows.UI.Core;

namespace SunTime;

public sealed partial class MainPage : Page
{
    private double _latitude;
    private double _longitude;
    private DispatcherTimer? _timer;
    private DispatcherTimer? _locationTimer;
#if DEBUG
    private DateTime? _debugUtcTime;
    private double _gpsLatitude;
    private double _gpsLongitude;
    private string _gpsLocationLabel = string.Empty;
    private bool _suppressSliderEvents;
    // Spring equinox ≈ March 20 = day 79
    private const int SpringEquinoxDayOfYear = 79;
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
            ? $"Stockholm (fallback)  {lat:F2}°N · {lon:F2}°E"
            : $"{lat:F2}°N · {lon:F2}°E";

        Refresh();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        // Refresh GPS every 15 minutes and persist the new fix.
        _locationTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
        _locationTimer.Tick += async (_, _) => await RefreshLocationAsync();
        _locationTimer.Start();

#if DEBUG
        _gpsLatitude      = _latitude;
        _gpsLongitude     = _longitude;
        _gpsLocationLabel = Dial.LocationLabel;
        DebugBar.Visibility = Microsoft.UI.Xaml.Visibility.Visible;

        // Date slider — day of year, default = today
        DateSlider.Value = DateTime.Now.DayOfYear;
        UpdateDebugDateText();
        DateSlider.ValueChanged += (_, _) =>
        {
            if (_suppressSliderEvents) return;
            UpdateDebugTimeFromSliders();
            UpdateDebugDateText();
            Refresh();
        };

        // Time slider — values represent UTC minutes (0-1439), independent of device timezone.
        // This ensures EQ at lon=0 shows 06:00–18:00 regardless of where the device is.
        TimeSlider.Value = DateTime.UtcNow.TimeOfDay.TotalMinutes;
        UpdateDebugTimeText();
        TimeSlider.ValueChanged += (_, _) =>
        {
            if (_suppressSliderEvents) return;
            UpdateDebugTimeFromSliders();
            UpdateDebugTimeText();
            Refresh();
        };

        // Lat / lon sliders — initialise to GPS fix
        LatSlider.Value = _latitude;
        LonSlider.Value = _longitude;
        UpdateDebugLatLonText();
        LatSlider.ValueChanged += (_, _) =>
        {
            if (_suppressSliderEvents) return;
            _latitude = LatSlider.Value;
            UpdateDebugLatLonText();
            Refresh();
        };
        LonSlider.ValueChanged += (_, _) =>
        {
            if (_suppressSliderEvents) return;
            _longitude = LonSlider.Value;
            UpdateDebugLatLonText();
            Refresh();
        };

        // EQ — equator, GMT, spring equinox, solar noon UTC
        DebugEqBtn.Click += (_, _) =>
        {
            Dial.SunHidden = true;
            _suppressSliderEvents = true;
            _latitude  = 0;
            _longitude = 0;
            LatSlider.Value  = 0;
            LonSlider.Value  = 0;
            DateSlider.Value = SpringEquinoxDayOfYear;
            TimeSlider.Value = 720;   // 12:00 UTC = solar noon at lon 0 on equinox
            _suppressSliderEvents = false;
            UpdateDebugLatLonText();
            UpdateDebugDateText();
            UpdateDebugTimeText();
            UpdateDebugTimeFromSliders();
            Refresh();
            Dial.SnapPosition();   // teleport sun immediately — no lerp across the dial
            Dial.SunHidden = false;
        };

        // Now — snap sliders to current values, then restore live tracking.
        // IMPORTANT: suppress events while setting sliders so ValueChanged can't
        // re-engage _debugUtcTime; null it out AFTER all assignments.
        DebugResetBtn.Click += (_, _) =>
        {
            Dial.SunHidden = true;
            _suppressSliderEvents = true;
            _latitude  = _gpsLatitude;
            _longitude = _gpsLongitude;
            LatSlider.Value  = _gpsLatitude;
            LonSlider.Value  = _gpsLongitude;
            DateSlider.Value = DateTime.Now.DayOfYear;
            TimeSlider.Value = DateTime.UtcNow.TimeOfDay.TotalMinutes;
            _suppressSliderEvents = false;
            _debugUtcTime = null;       // live tracking resumes — must be AFTER sliders
            UpdateDebugDateText();
            UpdateDebugTimeText();
            UpdateDebugLatLonText();
            Dial.LocationLabel = _gpsLocationLabel;
            Refresh();
            Dial.SnapPosition();
            Dial.SunHidden = false;
        };
#endif
    }

#if DEBUG
    private void UpdateDebugTimeText()
    {
        var minutes = (int)TimeSlider.Value;
        DebugTimeText.Text = $"{minutes / 60:D2}:{minutes % 60:D2}";
    }

    private void UpdateDebugLatLonText()
    {
        DebugLatText.Text = $"{LatSlider.Value:+0.0;-0.0}°";
        DebugLonText.Text = $"{LonSlider.Value:+0.0;-0.0}°";
        Dial.LocationLabel = $"{LatSlider.Value:+0.00;-0.00}°  {LonSlider.Value:+0.00;-0.00}°";
    }

    private void UpdateDebugTimeFromSliders()
    {
        var yearStart  = new DateTime(DateTime.Now.Year, 1, 1);
        var debugDate  = yearStart.AddDays((int)DateSlider.Value - 1);
        // TimeSlider is already UTC minutes — no device-timezone conversion needed.
        _debugUtcTime = DateTime.SpecifyKind(debugDate.AddMinutes(TimeSlider.Value), DateTimeKind.Utc);
    }

    private void UpdateDebugDateText()
    {
        var yearStart = new DateTime(DateTime.Now.Year, 1, 1);
        var date = yearStart.AddDays((int)DateSlider.Value - 1);
        DebugDateText.Text = date.ToString("MMM d");
    }
#endif

    private async Task RefreshLocationAsync()
    {
        var (lat, lon, isFallback) = await LocationService.GetLocationAsync();
        if (isFallback) return;   // no new fix — keep using whatever we have

        _latitude  = lat;
        _longitude = lon;

        var label = $"{lat:F2}°N · {lon:F2}°E";
        Dial.LocationLabel = label;

#if DEBUG
        // Keep GPS baseline in sync so Now snaps to the freshest fix.
        _gpsLatitude      = lat;
        _gpsLongitude     = lon;
        _gpsLocationLabel = label;
#endif

        Refresh();
    }

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Push a browser history entry so the browser back button fires BackRequested on WASM.
        SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
            AppViewBackButtonVisibility.Visible;
        Frame.Navigate(typeof(SettingsPage));
    }

    private void Refresh()
    {
#if DEBUG
        var utcNow = _debugUtcTime ?? DateTime.UtcNow;

        // In debug time mode use longitude-based solar offset so the dial
        // displays in solar time (e.g. EQ at lon=0 shows 06:00–18:00).
        // In live mode use the device's local wall-clock timezone as usual.
        var effectiveOffset = _debugUtcTime.HasValue
            ? TimeSpan.FromHours(_longitude / 15.0)
            : (SettingsService.AdjustApexForDst
                ? TimeZoneInfo.Local.GetUtcOffset(utcNow)
                : TimeZoneInfo.Local.BaseUtcOffset);

        var utcLastWeek    = utcNow - TimeSpan.FromDays(7);
        var lastWeekOffset = _debugUtcTime.HasValue
            ? TimeSpan.FromHours(_longitude / 15.0)
            : (SettingsService.AdjustApexForDst
                ? TimeZoneInfo.Local.GetUtcOffset(utcLastWeek)
                : TimeZoneInfo.Local.BaseUtcOffset);
#else
        var utcNow = DateTime.UtcNow;
        var effectiveOffset = SettingsService.AdjustApexForDst
            ? TimeZoneInfo.Local.GetUtcOffset(utcNow)
            : TimeZoneInfo.Local.BaseUtcOffset;

        var utcLastWeek    = utcNow - TimeSpan.FromDays(7);
        var lastWeekOffset = SettingsService.AdjustApexForDst
            ? TimeZoneInfo.Local.GetUtcOffset(utcLastWeek)
            : TimeZoneInfo.Local.BaseUtcOffset;
#endif

        var sun         = SolarCalculator.Calculate(_latitude, _longitude, utcNow,      effectiveOffset);
        var lastWeekSun = SolarCalculator.Calculate(_latitude, _longitude, utcLastWeek, lastWeekOffset);

        double solarNoonHour = sun.SolarNoon.Hour + sun.SolarNoon.Minute / 60.0 + sun.SolarNoon.Second / 3600.0;
        var moon    = MoonCalculator.Calculate(utcNow, solarNoonHour);
        var seasons = SeasonCalculator.Compute(utcNow.Year, _latitude, _longitude,
                          TimeZoneInfo.Local);

        var localNow = utcNow + effectiveOffset;
        Dial.Update(sun, localNow, lastWeekSun, moon, seasons);
    }
}
