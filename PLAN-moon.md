# Plan: Moon Phase on the Dial

## Goal
Show the moon on the 24-hour arc at its current position, rendered with the correct illuminated face (crescent, gibbous, full, etc.).

## Where it sits
The moon travels on the same arc as the sun — drawn inside `canvas.Save/Restore` so it rotates with NoonAtTop just like the sun disc. The moon's arc position is its approximate current solar-time equivalent, so at new moon it's near the sun, at full moon it's on the opposite side (12 h apart), at quarter moons it's 6 h away.

## Part 1 — MoonCalculator.cs (new file)

```
Services/MoonCalculator.cs
```

Two outputs from `DateTime utcNow`:

**Phase fraction** (0 → 1):
- Reference new moon: 2000-01-06 18:14 UTC (Julian day 2451550.26)
- Lunar period: 29.530588853 days
- `ageDays = (julianNow - refJD) mod period`
- `fraction = ageDays / period`   (0 = new, 0.25 = first quarter, 0.5 = full, 0.75 = last quarter)
- `illuminatedFraction = (1 − cos(fraction × 2π)) / 2`

**Moon's hour on the 24-h dial**:
The moon falls behind the sun by one full day per lunar cycle.
`moonHour = (solarNoonHour + fraction × 24) mod 24`
This is an approximation (±~30 min) — accurate enough for a decorative indicator.

Return type:
```csharp
public record MoonData(double PhaseFraction, double IlluminatedFraction, double HourOnDial);
```

## Part 2 — DrawMoon in SunDial.cs

Called inside `canvas.Save/Restore` (after `DrawSun`) so it participates in NoonAtTop rotation.

### Position
Same as the sun — place on the arc at `HourToAngle(moonData.HourOnDial)` at radius `R`.

### Moon disc size
Slightly smaller than the sun: `moonRadius = R * 0.055f` (sun is `R * 0.07f` above horizon).

### Rendering the illuminated face
The moon shape is built from two overlapping half-discs:

1. Draw the full grey disc (base).
2. The lit half is always the right or left semicircle depending on waxing/waning:
   - Waxing (fraction < 0.5): right side lit
   - Waning (fraction > 0.5): left side lit
3. Overlay a filled ellipse whose x-radius varies:
   - `ellipseX = moonRadius × |cos(fraction × 2π)|`
   - If quarter→full (illuminatedFraction > 0.5): ellipse is same colour as disc (additive)
   - If new→quarter (illuminatedFraction < 0.5): ellipse is dark (shadow, subtractive)

This produces all eight classical phases purely from geometry.

### Colours
- Lit face: `#E8E0C8` (warm white/ivory)
- Dark face: `#1A1A2E` (near-black, blends with night sky)
- Thin rim: 0.8px stroke `#C0B890 at 80α` to give a subtle halo

### When to skip
If moon hour is within ~15 min of sun hour (new moon overlap), skip drawing — they'd be on top of each other.

## Part 3 — Settings

**SettingsService.cs**: add `public static bool ShowMoon`  
**SettingsPage.xaml/.cs**: add "Show moon" toggle (same pattern as existing toggles)  
**SunDial.cs**: guard `DrawMoon` call with `if (SettingsService.ShowMoon)`

## Implementation order

1. `MoonCalculator.cs`
2. `SettingsService` + `SettingsPage` toggle
3. `DrawMoon` in `SunDial.cs`
4. Wire `_moonData` into `SunDial.Update()` / `Refresh()` in `MainPage.xaml.cs`

## Open questions / future
- Show moonrise/moonset times in small fields (like sunrise/sunset boxes)?
- Animate the moon moving across the arc in real time (it moves ~0.5°/h relative to the stars)?
- Show moon name / age in days as a tooltip or sub-field?
