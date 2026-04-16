using Android.App;
using Android.OS;
using Microsoft.UI.Xaml;

namespace SunTime.Droid;

[Activity(
    MainLauncher = true,
    ConfigurationChanges = global::Uno.UI.ActivityHelper.AllConfigChanges,
    WindowSoftInputMode = global::Android.Views.SoftInput.AdjustNothing | global::Android.Views.SoftInput.StateHidden
)]
public class MainActivity : Microsoft.UI.Xaml.ApplicationActivity
{
}

[global::Android.App.ApplicationAttribute(
    Label = "@string/ApplicationName",
    Icon = "@mipmap/iconapp",
    Theme = "@style/AppTheme",
    Debuggable = false
)]
public class App : Microsoft.UI.Xaml.NativeApplication
{
    public App(IntPtr javaReference, global::Android.Runtime.JniHandleOwnership transfer)
        : base(() => new SunTime.App(), javaReference, transfer)
    {
        ConfigureUniversalImageLoader();
    }

    private static void ConfigureUniversalImageLoader()
    {
    }
}
