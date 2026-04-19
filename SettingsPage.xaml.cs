using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SunTime.Services;

namespace SunTime;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        // Sync all toggles to persisted values without triggering their handlers.
        SetToggle(DstToggle,            DstToggle_Toggled,            SettingsService.AdjustApexForDst);
        SetToggle(NoonAtTopToggle,      NoonAtTopToggle_Toggled,      SettingsService.NoonAtTop);
        SetToggle(ShowDurationsToggle,  ShowDurationsToggle_Toggled,  SettingsService.ShowDurations);
        SetToggle(ShowApexTimeToggle,   ShowApexTimeToggle_Toggled,   SettingsService.ShowApexTime);
        SetToggle(ShowWeekDiffsToggle,  ShowWeekDiffsToggle_Toggled,  SettingsService.ShowWeekDiffs);
        SetToggle(ShowSunAngleToggle,   ShowSunAngleToggle_Toggled,   SettingsService.ShowSunAngle);
    }

    private static void SetToggle(ToggleSwitch toggle, RoutedEventHandler handler, bool value)
    {
        toggle.Toggled -= handler;
        toggle.IsOn = value;
        toggle.Toggled += handler;
    }

    private void DstToggle_Toggled(object sender, RoutedEventArgs e) =>
        SettingsService.AdjustApexForDst = DstToggle.IsOn;

    private void NoonAtTopToggle_Toggled(object sender, RoutedEventArgs e) =>
        SettingsService.NoonAtTop = NoonAtTopToggle.IsOn;

    private void ShowDurationsToggle_Toggled(object sender, RoutedEventArgs e) =>
        SettingsService.ShowDurations = ShowDurationsToggle.IsOn;

    private void ShowApexTimeToggle_Toggled(object sender, RoutedEventArgs e) =>
        SettingsService.ShowApexTime = ShowApexTimeToggle.IsOn;

    private void ShowWeekDiffsToggle_Toggled(object sender, RoutedEventArgs e) =>
        SettingsService.ShowWeekDiffs = ShowWeekDiffsToggle.IsOn;

    private void ShowSunAngleToggle_Toggled(object sender, RoutedEventArgs e) =>
        SettingsService.ShowSunAngle = ShowSunAngleToggle.IsOn;

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
            Frame.GoBack();
    }
}
