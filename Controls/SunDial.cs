using System;
using Microsoft.UI.Dispatching;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using SunTime.Services;

namespace SunTime.Controls;

public class SunDial : SKXamlCanvas
{
    // Large clipping extent so the half-sun clip can extend well beyond the canvas bounds.
    private const float SymbolClipExtent = 10000f;

    private enum HorizonSymbolType
    {
        None,
        Sunrise,
        Sunset
    }

    private SolarCalculator.SunData? _sun;
    private SolarCalculator.SunData? _yesterdaySun;
    private DateTime _localNow;
    private DateTime _animatedLocalNow;
    private readonly DispatcherTimer _animTimer;

    public SunDial()
    {
        PaintSurface += OnPaint;

        _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _animTimer.Tick += (_, _) =>
        {
            if (_sun is null) return;
            var diff = _localNow - _animatedLocalNow;
            if (diff.TotalSeconds < 0.5 && diff.TotalSeconds > -0.5)
            {
                _animatedLocalNow = _localNow;
            }
            else
            {
                _animatedLocalNow += TimeSpan.FromTicks((long)(diff.Ticks * 0.08));
            }
            Invalidate();
        };
        _animTimer.Start();
    }

    public void Update(SolarCalculator.SunData sun, DateTime localNow, SolarCalculator.SunData? yesterdaySun = null)
    {
        _sun = sun;
        _localNow = localNow;
        _yesterdaySun = yesterdaySun;
        // Seed animated time on first call to avoid lerp from epoch
        if (_animatedLocalNow == default) _animatedLocalNow = localNow;
    }

    // ── paint entry ─────────────────────────────────────────

    private void OnPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(new SKColor(0x12, 0x12, 0x20)); // dark background

        if (_sun is null) return;

        var info = e.Info;
        float size = Math.Min(info.Width, info.Height);
        float cx = info.Width / 2f;
        float cy = info.Height * 0.45f;
        float R = size * 0.34f;

        DrawSkyGround(canvas, cx, cy, R, _sun);
        DrawHorizonLine(canvas, cx, cy, R);
        DrawClockFace(canvas, cx, cy, R * 0.58f);
        DrawEventMarkers(canvas, cx, cy, R);
        DrawSun(canvas, cx, cy, R);
    }

    // ── sky / ground arc ────────────────────────────────────

    private static void DrawSkyGround(SKCanvas c, float cx, float cy, float r,
                                        SolarCalculator.SunData sun)
    {
        var rect = new SKRect(cx - r, cy - r, cx + r, cy + r);

        // Convert rise/set hours to SkiaSharp degrees (0° = 3-o'clock, clockwise)
        float riseSkia = HourToSkiaDeg(sun.Sunrise);
        float setSkia = HourToSkiaDeg(sun.Sunset);
        float daySweep = (setSkia - riseSkia + 360f) % 360f;
        float nightSweep = 360f - daySweep;

        // Day wedge (sunrise → sunset through noon)
        using var skyPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx, cy), r,
                new[] { new SKColor(0x4A, 0x90, 0xD9), new SKColor(0x87, 0xCE, 0xEB) },
                null, SKShaderTileMode.Clamp)
        };
        using var skyPath = new SKPath();
        skyPath.MoveTo(cx, cy);
        skyPath.ArcTo(rect, riseSkia, daySweep, false);
        skyPath.Close();
        c.DrawPath(skyPath, skyPaint);

        // Night wedge (sunset → sunrise through midnight)
        using var groundPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx, cy), r,
                new[] { new SKColor(0x1A, 0x23, 0x4D), new SKColor(0x0D, 0x11, 0x2B) },
                null, SKShaderTileMode.Clamp)
        };
        using var groundPath = new SKPath();
        groundPath.MoveTo(cx, cy);
        groundPath.ArcTo(rect, setSkia, nightSweep, false);
        groundPath.Close();
        c.DrawPath(groundPath, groundPaint);

        // Thin ring outline
        using var ringPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            Color = new SKColor(0xFF, 0xFF, 0xFF, 0x40)
        };
        c.DrawCircle(cx, cy, r, ringPaint);
    }

    /// <summary>Convert a DateTime's hour+minute to SkiaSharp degrees (0°=right, clockwise).</summary>
    private static float HourToSkiaDeg(DateTime t)
    {
        double hour = t.Hour + t.Minute / 60.0;
        return (float)(((hour / 24.0) * 360.0 - 270.0 + 360.0) % 360.0);
    }

    private static void DrawHorizonLine(SKCanvas c, float cx, float cy, float r)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Color = new SKColor(0xFF, 0xFF, 0xFF, 0x80),
            PathEffect = SKPathEffect.CreateDash(new[] { 6f, 4f }, 0)
        };
        c.DrawLine(cx - r, cy, cx + r, cy, paint);
    }

    // ── 24-hour clock face ──────────────────────────────────

    private void DrawClockFace(SKCanvas c, float cx, float cy, float r)
    {
        using var tickPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Color = new SKColor(0xFF, 0xFF, 0xFF, 0x60)
        };
        using var numFont = new SKFont(
            SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            r * 0.18f);
        using var numPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0xFF, 0xFF, 0xFF, 0xB0)
        };

        for (int h = 0; h < 24; h++)
        {
            double a = HourToAngle(h);
            float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);

            float inner = h % 6 == 0 ? 0.82f : 0.90f;
            c.DrawLine(cx + r * inner * cos, cy - r * inner * sin,
                       cx + r * cos, cy - r * sin, tickPaint);

            if (h % 3 == 0)
            {
                float nr = r * 0.72f;
                float tx = cx + nr * cos;
                float ty = cy - nr * sin + numFont.Size * 0.35f;
                c.DrawText(h.ToString(), tx, ty, SKTextAlign.Center, numFont, numPaint);
            }
        }

        // Hour hand
        double nowAngle = HourToAngle(_animatedLocalNow.Hour + _animatedLocalNow.Minute / 60.0
                                        + _animatedLocalNow.Second / 3600.0);
        using var handPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3f,
            StrokeCap = SKStrokeCap.Round,
            Color = new SKColor(0xFF, 0xD7, 0x00)
        };
        float hLen = r * 0.55f;
        c.DrawLine(cx, cy,
                   cx + hLen * (float)Math.Cos(nowAngle),
                   cy - hLen * (float)Math.Sin(nowAngle), handPaint);

        // Center dot
        using var dotPaint = new SKPaint { IsAntialias = true, Color = SKColors.White };
        c.DrawCircle(cx, cy, 3f, dotPaint);
    }

    // ── sunrise / sunset / noon markers ─────────────────────

    private void DrawEventMarkers(SKCanvas c, float cx, float cy, float R)
    {
        if (_sun is null) return;

        double? riseGainPerWeek = null, setGainPerWeek = null;
        if (_yesterdaySun is not null)
        {
            var riseDelta = (_sun.Sunrise.TimeOfDay - _yesterdaySun.Sunrise.TimeOfDay).TotalMinutes;
            var setDelta = (_sun.Sunset.TimeOfDay - _yesterdaySun.Sunset.TimeOfDay).TotalMinutes;
            // Earlier sunrise is a daylight gain, so invert sunrise delta sign.
            riseGainPerWeek = -riseDelta * 7.0;
            setGainPerWeek = setDelta * 7.0;
        }

        // Gain/loss sectors between yesterday and today event angles (behind today's markers)
        if (_yesterdaySun is not null)
        {
            DrawDiffSector(c, cx, cy, R, _yesterdaySun.Sunrise, _sun.Sunrise, riseGainPerWeek);
            DrawDiffSector(c, cx, cy, R, _yesterdaySun.Sunset, _sun.Sunset, setGainPerWeek);
        }

        DrawMarker(c, cx, cy, R, _sun.Sunrise, $"{_sun.Sunrise:HH:mm}", new SKColor(0xFF, 0xA5, 0x00), riseGainPerWeek, horizonSymbol: HorizonSymbolType.Sunrise);
        DrawMarker(c, cx, cy, R, _sun.Sunset, $"{_sun.Sunset:HH:mm}", new SKColor(0xFF, 0x63, 0x47), setGainPerWeek, horizonSymbol: HorizonSymbolType.Sunset);
        DrawMarker(c, cx, cy, R, _sun.SolarNoon, $"☀ {_sun.SolarNoon:HH:mm}", new SKColor(0xFF, 0xD7, 0x00));
    }

    private static void DrawMarker(SKCanvas c, float cx, float cy, float R,
                                    DateTime time, string labelText, SKColor color,
                                    double? deltaMinutes = null,
                                    HorizonSymbolType horizonSymbol = HorizonSymbolType.None)
    {
        double a = HourToAngle(time.Hour + time.Minute / 60.0);
        float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);

        // Small mark on the arc
        using var markPaint = new SKPaint
        {
            IsAntialias = true,
            Color = color,
            Style = SKPaintStyle.Fill
        };
        c.DrawCircle(cx + R * cos, cy - R * sin, 5f, markPaint);

        // Time label just outside
        using var labelFont = new SKFont(SKTypeface.FromFamilyName("Arial"), R * 0.11f);
        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            Color = color
        };
        float lr = R * 1.22f;
        float lx = cx + lr * cos;
        float ly = cy - lr * sin + labelFont.Size * 0.35f;

        if (horizonSymbol != HorizonSymbolType.None)
        {
            float symbolSize = labelFont.Size * 0.78f;
            float spacing = labelFont.Size * 0.28f;
            float textWidth = labelFont.MeasureText(labelText);
            float totalWidth = symbolSize + spacing + textWidth;
            float startX = lx - totalWidth * 0.5f;
            float symbolCx = startX + symbolSize * 0.5f;
            float symbolCy = ly - labelFont.Size * 0.35f;

            DrawSunHorizonSymbol(c, symbolCx, symbolCy, symbolSize, color, isSunrise: horizonSymbol == HorizonSymbolType.Sunrise);
            c.DrawText(labelText, startX + symbolSize + spacing, ly, SKTextAlign.Left, labelFont, textPaint);
        }
        else
        {
            c.DrawText(labelText, lx, ly, SKTextAlign.Center, labelFont, textPaint);
        }

        // Delta text below the label
        if (deltaMinutes.HasValue)
        {
            int rounded = (int)Math.Round(deltaMinutes.Value);
            if (rounded != 0)
            {
                string deltaStr = $"{(rounded > 0 ? "+" : "")}{rounded}m/wk";
                var deltaColor = rounded > 0
                    ? new SKColor(0x66, 0xBB, 0x6A)  // green
                    : new SKColor(0xEF, 0x53, 0x50);  // red

                using var deltaFont = new SKFont(SKTypeface.FromFamilyName("Arial"), R * 0.10f);
                using var deltaPaint = new SKPaint
                {
                    IsAntialias = true,
                    Color = deltaColor
                };
                c.DrawText(deltaStr, lx, ly + labelFont.Size * 0.9f,
                           SKTextAlign.Center, deltaFont, deltaPaint);
            }
        }
    }

    private static void DrawSunHorizonSymbol(SKCanvas c, float cx, float cy, float size, SKColor color, bool isSunrise)
    {
        float halfWidth = size * 0.5f;
        float horizonY = cy;
        float radius = size * 0.32f;
        float sunCenterY = isSunrise ? horizonY - radius * 0.85f : horizonY + radius * 0.85f;

        using var linePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeWidth = Math.Max(1.25f, size * 0.10f),
            Color = color
        };
        c.DrawLine(cx - halfWidth, horizonY, cx + halfWidth, horizonY, linePaint);

        using var sunPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = color
        };
        using var clip = new SKPath();
        if (isSunrise)
        {
            clip.AddRect(new SKRect(cx - SymbolClipExtent, -SymbolClipExtent, cx + SymbolClipExtent, horizonY));
        }
        else
        {
            clip.AddRect(new SKRect(cx - SymbolClipExtent, horizonY, cx + SymbolClipExtent, SymbolClipExtent));
        }

        c.Save();
        c.ClipPath(clip, SKClipOperation.Intersect, true);
        c.DrawCircle(cx, sunCenterY, radius, sunPaint);
        c.Restore();
    }

    // ── sun icon ────────────────────────────────────────────

    private void DrawSun(SKCanvas c, float cx, float cy, float R)
    {
        if (_sun is null) return;

        double fraction = _animatedLocalNow.TimeOfDay.TotalHours / 24.0;
        double a = HourToAngle(fraction * 24.0);
        float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
        float sx = cx + R * cos;
        float sy = cy - R * sin;

        bool aboveHorizon = _sun.Altitude >= 0;
        float sunRadius = aboveHorizon ? R * 0.07f : R * 0.045f;
        byte alpha = aboveHorizon ? (byte)0xFF : (byte)0x80;

        // Glow
        using var glowPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(sx, sy), sunRadius * 3,
                new[] { new SKColor(0xFF, 0xD7, 0x00, (byte)(alpha / 3)), SKColors.Transparent },
                null, SKShaderTileMode.Clamp)
        };
        c.DrawCircle(sx, sy, sunRadius * 3, glowPaint);

        // Sun disc
        using var sunPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(sx - sunRadius * 0.3f, sy - sunRadius * 0.3f), sunRadius,
                new[] { new SKColor(0xFF, 0xF1, 0x76, alpha), new SKColor(0xFF, 0xA5, 0x00, alpha) },
                null, SKShaderTileMode.Clamp)
        };
        c.DrawCircle(sx, sy, sunRadius, sunPaint);

        // Altitude label following the sun
        using var altFont = new SKFont(SKTypeface.FromFamilyName("Arial"), R * 0.08f);
        using var altPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0xFF, 0xFF, 0xFF, 0xCC)
        };
        float altR = R * 1.12f;
        string altText = $"{_sun.Altitude:F1}°";
        c.DrawText(altText, cx + altR * cos, cy - altR * sin + altFont.Size * 0.35f,
                   SKTextAlign.Center, altFont, altPaint);
    }

    private static void DrawDiffSector(SKCanvas c, float cx, float cy, float R,
                                       DateTime yesterdayTime, DateTime todayTime,
                                       double? gainMinutes)
    {
        if (!gainMinutes.HasValue) return;

        int roundedGain = (int)Math.Round(gainMinutes.Value);
        if (roundedGain == 0) return;

        float yesterdaySkia = HourToSkiaDeg(yesterdayTime);
        float todaySkia = HourToSkiaDeg(todayTime);
        // Normalize to [0, 360), then remap to shortest signed sweep so sectors cross 0° correctly.
        float normalizedSweep = (todaySkia - yesterdaySkia + 360f) % 360f;
        float sweep = normalizedSweep > 180f ? normalizedSweep - 360f : normalizedSweep;
        if (Math.Abs(sweep) < 0.01f) return;

        var fillColor = roundedGain > 0
            ? new SKColor(0x66, 0xBB, 0x6A, 0x44)
            : new SKColor(0xEF, 0x53, 0x50, 0x44);

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = fillColor
        };
        var rect = new SKRect(cx - R, cy - R, cx + R, cy + R);
        using var path = new SKPath();
        path.MoveTo(cx, cy);
        path.ArcTo(rect, yesterdaySkia, sweep, false);
        path.Close();
        c.DrawPath(path, paint);
    }

    // ── angle helpers ───────────────────────────────────────

    /// <summary>
    /// Maps an hour (0-24) to a math-convention angle in radians.
    /// 12:00 → top (π/2), 6:00 → left (π), 0/24 → bottom (3π/2), 18:00 → right (0).
    /// </summary>
    private static double HourToAngle(double hour)
    {
        double fraction = hour / 24.0;
        double degrees = 270.0 - fraction * 360.0;
        return degrees * Math.PI / 180.0;
    }
}
