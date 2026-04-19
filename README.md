# SunTime

A WebAssembly app that visualises the sun's current position relative to the horizon, built with [Uno Platform](https://platform.uno/) and SkiaSharp.

## Features

- **Sun dial visualisation** – a circular view with a sky/ground split. The sun icon travels along the outer arc from sunrise to sunset, and dims below the horizon at night.
- **Smooth animation** – the sun icon lerps toward the current time on every animation frame (~30 fps), so positional updates feel fluid rather than instant.
- **24-hour analog clock** – tick marks and hour labels (0–23) drawn inside the orbit ring, with a live hour and minute hand. The second hand is driven by real wall-clock time so it keeps ticking between 1-minute refreshes.
- **Sunrise / sunset / noon markers** – colour-coded time labels inside the ring (orange for rise, tomato for set, gold for solar noon), pinned to fixed upper-quadrant positions so they always sit in the sky half.
- **Day-over-day diff sectors** – green/red arcs on the ring show how sunrise and sunset have shifted compared to the previous day, with ±m/wk annotations.
- **Solar calculations** – NOAA solar position algorithm computing sunrise, sunset, solar noon, and current altitude for any location.
- **Geolocation** – uses the browser's Geolocation API via `Windows.Devices.Geolocation.Geolocator`. Falls back to Stockholm (59.33°N, 18.07°E) if permission is denied. Location is shown inside the clock face.
- **Watch-style layout** – the dial fills the full screen with rendered top and bottom wristbands (3-D gradient, stitching, texture ribs, punch holes, and pin buckle).
- **Animated splash screen** – a sun-rise animation plays on startup before navigating to the main dial.
- **Auto-refresh** – a 1-minute timer keeps the dial in sync with the current time.

## Settings

Tap the **⚙** button (top-right overlay) to open the settings page. All settings are persisted via `localStorage` (mapped by Uno's `ApplicationData.LocalSettings`).

| Setting | Description |
|---------|-------------|
| Adjust for DST | Shifts solar noon to match the clock during daylight saving time. |
| Solar noon at top | Rotates the entire clock so solar noon always sits at the 12 o'clock position. |
| Show durations | Displays daylight and nighttime length inside the ring. |
| Apex time | Shows the solar noon time label (the marker dot is always visible). |
| Week diffs | Shows the day-over-day arc sectors and ±m/wk shift annotations. |
| Sun angle | Shows the current solar elevation near the sun icon. |

## Tech Stack

| | |
|---|---|
| Framework | [Uno Platform](https://platform.uno/) with `Uno.Sdk` 6.5.31 |
| Target | `net10.0-browserwasm` |
| Rendering | SkiaSharp (`SKXamlCanvas`) via the `SkiaRenderer` UnoFeature |

## Project Structure

```
SunTime/
├── Controls/
│   └── SunDial.cs           # SkiaSharp custom control
├── Services/
│   ├── LocationService.cs   # Browser geolocation wrapper
│   ├── SettingsService.cs   # User preferences (6 settings)
│   └── SolarCalculator.cs   # NOAA sun position algorithm
├── Platforms/
│   └── WebAssembly/
│       └── Program.cs       # WASM entry point
├── MainPage.xaml/.cs        # Main UI + refresh timer; ⚙ button navigates to SettingsPage
├── SettingsPage.xaml/.cs    # Settings page with all toggles and ← Back navigation
├── SplashPage.xaml/.cs      # Splash / loading screen
├── App.xaml/.cs             # App entry point
└── SunTime.csproj
```

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Uno Platform workload](https://platform.uno/docs/articles/get-started.html)

```bash
dotnet workload install uno-wasm
```

### Run

```bash
dotnet run
```

The app will be served at `http://localhost:5001`.

## Download build output from GitHub Actions

Each CI run uploads a downloadable artifact named `suntime-browserwasm`.

1. Open the **Actions** tab in GitHub.
2. Select a **Build** workflow run.
3. Download the `suntime-browserwasm` artifact from the **Artifacts** section.

## Cloud environment (Oz)

This repo is wired up to a Warp Oz cloud environment (team-scoped, ID `DMT1HA3vyEQmfeYplc8Xo0`) so cloud agents can build and run against both target frameworks declared in `SunTime.csproj` (`net10.0-browserwasm;net10.0-android`).

### Base image

`mcr.microsoft.com/dotnet/sdk:10.0`

### Setup commands (order matters)

1. `apt-get update && apt-get install -y python3`
2. `dotnet workload install wasm-tools`
3. `dotnet workload install android`
4. `cd /workspace/SunTime && dotnet restore`

The `android` workload install is required because `dotnet restore` with no `-f` flag resolves every TFM in the csproj, and the `net10.0-android` target fails without the workload. Symptom when it's missing:

```
Environment setup failed: Failed to run setup command: cd /workspace/SunTime && dotnet restore.
```

### Updating the environment

Use the `oz` CLI (ordering is append-only, so you may need to remove and re-add the `dotnet restore` step to keep it last):

```bash
oz environment update DMT1HA3vyEQmfeYplc8Xo0 \
  --remove-setup-command "cd /workspace/SunTime && dotnet restore" \
  -c "dotnet workload install android" \
  -c "cd /workspace/SunTime && dotnet restore"
```

Verify:

```bash
oz environment get DMT1HA3vyEQmfeYplc8Xo0 --output-format text
```
