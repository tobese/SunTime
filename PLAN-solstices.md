# Plan: Solstice & Equinox Ring

Show the four solar calendar events — summer solstice, winter solstice, spring equinox, autumn equinox — as markers on a thin inner ring just inside the orbit arc, giving the dial a year-in-a-glance dimension alongside the day-in-a-glance sun position.

## Core idea

Add a thin decorative ring at radius `R * 0.92` (just inside the sun's orbit at `R`). On this ring, place four markers at the angular positions corresponding to each event's solar noon time — since the solstices/equinoxes have characteristic noon altitudes, their positions on a 24h dial cluster near the top (noon) but their *daylight arc widths* tell the story.

A more informative approach: show the **sunrise and sunset positions** for each of the four events as arc segments on the inner ring, so the user can see at a glance how daylight expands and contracts across the year.

## Visual design

| Element | Detail |
|---------|--------|
| Inner ring | Thin stroke circle at `R * 0.92`, subtle white/20% opacity |
| Summer solstice arc | Longest day — wide gold arc from its rise to set angle |
| Winter solstice arc | Shortest day — narrow blue arc |
| Spring equinox arc | ~12h day — green tick/dot at rise and set |
| Autumn equinox arc | ~12h day — amber tick/dot at rise and set |
| Today arc | The current day's rise→set arc already shown by sky fill, but could add a subtle highlight on the inner ring |

Arcs are drawn as `SKCanvas.DrawArc` strokes on the inner ring, colour-coded by season.

## Data

`SolarCalculator` already computes sunrise/sunset for any date. Add a helper:

```csharp
// In SolarCalculator
public static SunData ComputeForDate(DateTime date, double lat, double lon)
```

Then in `SunDial.Update` (or `MainPage.Refresh`), compute and cache the four event dates:

| Event | ~Date |
|-------|-------|
| Spring equinox | ~March 20 |
| Summer solstice | ~June 21 |
| Autumn equinox | ~September 22 |
| Winter solstice | ~December 21 |

Exact dates shift by ±1–2 days per year; use a simple lookup table keyed on year, or compute via an approximate formula (good enough for display).

## Implementation steps

1. **`GetSolsticeEquinoxDates(int year)`** — returns the four `DateTime` values for the current year using a Meeus-style approximation (no external dependency needed).

2. **`SolarCalculator.ComputeForDate(DateTime, double, double)`** — reuse existing NOAA logic, just with a different date. Cache results in `SunDial` alongside `_sun` and `_yesterdaySun`.

3. **`DrawSeasonRing(SKCanvas, cx, cy, R, seasonData[])`** — draws:
   - The thin inner ring stroke
   - Four arcs (or tick pairs) for each event's daylight window
   - Small season icons or initials (S/W/≡) near each arc midpoint, optional

4. **Call site** — `DrawSeasonRing` inside the `canvas.Save/Restore` rotation block so it rotates with NoonAtTop.

5. **Settings toggle** — `ShowSeasonRing` (default on) added to `SettingsService` and `SettingsPage`.

## Positioning note

The arc midpoint for each event falls at the solar noon angle (top of dial for standard view, or wherever noon maps to). The arc width reflects day length — summer solstice will be a wide ~270° arc in Stockholm, winter will be narrow ~90°. This makes the seasonal rhythm immediately readable.

## Smoothness / performance

All four data points are static for a given date and location — compute once on refresh, not per frame.
