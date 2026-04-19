# Plan: Dynamic Sky Background

Drive the full-canvas background colour from the sun's altitude angle, replacing the flat dark fill with a vertical gradient that shifts from deep night through twilight to bright day.

## Core idea

Replace `canvas.Clear(#121220)` with a 3-stop vertical gradient whose colours are computed from `sun.Altitude`. The three stops are:

| Stop | Position | What it represents |
|------|----------|--------------------|
| Zenith | y = 0 (top) | Sky overhead |
| Horizon | y = cy (dial centre) | Sky at the horizon |
| Ground | y = canvas height | Below-horizon fill |

Sunset and sunrise are handled automatically — both are just "altitude near 0°" so the same warm keyframe applies to both.

## Colour keyframes

| Altitude | Zenith | Horizon | Ground |
|----------|--------|---------|--------|
| −18° deep night | `#060610` | `#080818` | `#050510` |
| −6° civil twilight | `#0D1035` | `#18154A` | `#0A0B20` |
|  0° at horizon | `#1A2050` | `#E8602A` | `#14121E` |
| +5° just risen | `#1E3A6E` | `#FFA040` | `#161825` |
| +15° morning | `#2A5F9E` | `#7BB8D4` | `#1A2035` |
| +45° midday | `#4A90D9` | `#87CEEB` | `#1E2840` |

## Implementation steps

1. **`LerpSkyPalette(double altitude)`** — static helper in `SunDial.cs`.  
   Walks the keyframe table, finds the two surrounding entries, and linearly interpolates each colour channel. Returns `(zenith, horizon, ground)`.

2. **`DrawBackground(SKCanvas, float w, float h, float cy, palette)`** — draws a 3-stop `SKShader.CreateLinearGradient` vertically across the full canvas.

3. **`OnPaint`** — call `DrawBackground` first (before straps and dial), replacing `canvas.Clear`.

4. **Straps** — keep their current fixed dark colour; physical material doesn't react to ambient light.

## Smoothness

Altitude changes at most ~0.25°/min, so the 1-minute refresh produces imperceptibly small colour steps. No extra animation is needed.
