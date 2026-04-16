# SunTime – Sun Position Visualizer
## Problem
Build a WASM app that shows the sun's current position relative to the user's horizon, using a circular visualization with a 24-hour analog clock.
## Current State
* The project lives at `/Users/tb/Dev/WordFinder/SunTime` (empty directory).
* The sibling WordFinder project uses Uno.Sdk 6.5.31, `net10.0-browserwasm`, and the `SkiaRenderer` UnoFeature. We'll mirror this setup.
* Uno's `Windows.Devices.Geolocation.Geolocator` is supported on WASM (uses the browser's Geolocation API).
* SkiaSharp drawing is available via `SKXamlCanvas` from the `SkiaSharp.Views.Uno.WinUI` package.
## Proposed Changes
### 1. Project Scaffolding
Create a standalone Uno WASM project under `SunTime/`:
* `SunTime.csproj` – Uno.Sdk, `net10.0-browserwasm`, UnoFeatures: `SkiaRenderer`
* `global.json` – Uno.Sdk 6.5.31 (same as WordFinder)
* `Directory.Build.props`, `GlobalUsings.cs`, `App.xaml/.cs` – standard Uno boilerplate mirroring WordFinder
* `Properties/launchSettings.json` – use port 5001 (to avoid conflict with WordFinder on 5000)
* Add NuGet reference to `SkiaSharp.Views.Uno.WinUI` for custom drawing
### 2. Sun Calculation Service (`Services/SolarCalculator.cs`)
Implement the NOAA solar position algorithm in a self-contained static class. No external NuGet dependency needed – the math is straightforward.
Key outputs for a given (latitude, longitude, DateTime UTC):
* **Sunrise** and **Sunset** times (local)
* **Solar noon** (apex) time
* **Current sun altitude** (degrees above/below horizon, negative = below)
* **Hour angle** (to position the sun on the circular arc)
The algorithm uses Julian date, solar declination, equation of time, and hour angle formulas from the NOAA spreadsheet.
### 3. Geolocation Service (`Services/LocationService.cs`)
Use `Windows.Devices.Geolocation.Geolocator` to:
* Request access and obtain the user's lat/lon.
* Provide a fallback location (e.g. Stockholm 59.33°N, 18.07°E) if geolocation is denied.
* Expose the location as a simple `(Latitude, Longitude)` tuple.
### 4. Visualization (`Controls/SunDial.cs` – SKXamlCanvas subclass)
Custom SkiaSharp-drawn control with the following elements:
* **Outer circle**: upper half light sky-blue gradient, lower half dark navy gradient. The dividing horizontal diameter = horizon.
* **Sun icon**: a filled yellow/orange circle positioned along the outer arc. Its angle is derived from the current time mapped between sunrise/sunset. Below-horizon sun is drawn dimmer/smaller.
* **24-hour analog clock face** inside the orbit: 24 tick marks, hour numbers (0–23), with a single hour hand pointing to the current time.
* **Markers & labels**: small marks on the outer arc at sunrise, sunset, and solar noon positions, with their times as text labels.
* **Info text**: sunrise/sunset/noon times and current altitude displayed as text below or inside the dial.
### 5. Main Page (`MainPage.xaml / .cs`)
* Center the `SunDial` control.
* On load: request geolocation, compute initial sun data, start a `DispatcherTimer` (1-minute tick) to refresh the dial.
* Show a brief status ("Locating…" / location name or coords) above/below the dial.
### Visual Layout (ASCII sketch)
```warp-runnable-command
          ☀ (apex/noon)
       ╭───────────╮
      ╱  light sky   ╲
     │   ┌────────┐   │
 sunrise─┤ 24h clk ├──sunset
     │   └────────┘   │
      ╲  dark ground ╱
       ╰─────────────╯
    Sunrise: 05:32  Sunset: 20:14
    Solar noon: 12:53  Alt: 42.3°
```
### File Summary
* `SunTime.csproj`, `global.json`, `Directory.Build.props` – project config
* `App.xaml` / `App.xaml.cs` – app entry point
* `GlobalUsings.cs` – common usings
* `Properties/launchSettings.json` – dev server config
* `Services/SolarCalculator.cs` – sun math
* `Services/LocationService.cs` – geolocation wrapper
* `Controls/SunDial.cs` – SkiaSharp visualization
* `MainPage.xaml` / `MainPage.xaml.cs` – main UI and timer logic
