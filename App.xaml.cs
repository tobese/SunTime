using System;
using Microsoft.Extensions.Logging;
using Windows.UI.Core;

namespace SunTime;

public partial class App : Application
{
    public App()
    {
        this.InitializeComponent();
    }

    protected Window? MainWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new Window();
#if DEBUG
        MainWindow.UseStudio();
#endif

        if (MainWindow.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            MainWindow.Content = rootFrame;
            rootFrame.NavigationFailed += (s, e) =>
                throw new InvalidOperationException($"Failed to load {e.SourcePageType.FullName}: {e.Exception}");
        }

        if (rootFrame.Content == null)
        {
            rootFrame.Navigate(typeof(SplashPage), args.Arguments);
        }

        // Handle browser back button (WASM) and hardware back (Android) at the app level.
        // AppViewBackButtonVisibility = Visible (set when entering Settings) pushes a browser
        // history entry via history.pushState, which makes BackRequested fire on browser back.
        var navMgr = SystemNavigationManager.GetForCurrentView();
        navMgr.BackRequested += (_, e) =>
        {
            if (rootFrame.BackStack.Count > 0)
            {
                e.Handled = true;
                rootFrame.GoBack();
                if (rootFrame.BackStack.Count == 0)
                    navMgr.AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
            }
        };

        MainWindow.Activate();
    }

    public static void InitializeLogging()
    {
#if DEBUG
        var factory = LoggerFactory.Create(builder =>
        {
#if __WASM__
            builder.AddProvider(new global::Uno.Extensions.Logging.WebAssembly.WebAssemblyConsoleLoggerProvider());
#else
            builder.AddConsole();
#endif
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddFilter("Uno", LogLevel.Warning);
            builder.AddFilter("Windows", LogLevel.Warning);
            builder.AddFilter("Microsoft", LogLevel.Warning);
        });

        global::Uno.Extensions.LogExtensionPoint.AmbientLoggerFactory = factory;

#if HAS_UNO
        global::Uno.UI.Adapter.Microsoft.Extensions.Logging.LoggingAdapter.Initialize();
#endif
#endif
    }
}
