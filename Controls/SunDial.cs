using System;
using Microsoft.UI.Dispatching;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using SunTime.Services;

namespace SunTime.Controls;

public class SunDial : SKXamlCanvas
{
    private enum MarkerType { Rise, Set, Noon }
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

        double? riseDelta = null, setDelta = null;
        if (_yesterdaySun is not null)
        {
            riseDelta = (_sun.Sunrise.TimeOfDay - _yesterdaySun.Sunrise.TimeOfDay).TotalMinutes;
            setDelta = (_sun.Sunset.TimeOfDay - _yesterdaySun.Sunset.TimeOfDay).TotalMinutes;
        }

        // Green/red diff sectors (behind today's markers)
        if (_yesterdaySun is not null)
        {
            DrawDiffSector(c, cx, cy, R, _yesterdaySun.Sunrise, _sun.Sunrise, invertGain: true);
            DrawDiffSector(c, cx, cy, R, _yesterdaySun.Sunset,  _sun.Sunset,  invertGain: false);
        }

        DrawMarker(c, cx, cy, R, _sun.Sunrise,   MarkerType.Rise, new SKColor(0xFF, 0xA5, 0x00), riseDelta, invertDelta: true);
        DrawMarker(c, cx, cy, R, _sun.Sunset,    MarkerType.Set,  new SKColor(0xFF, 0x63, 0x47), setDelta,  invertDelta: false);
        DrawMarker(c, cx, cy, R, _sun.SolarNoon, MarkerType.Noon, new SKColor(0xFF, 0xD7, 0x00));
    }

    private static void DrawMarker(SKCanvas c, float cx, float cy, float R,
                                    DateTime time, MarkerType type, SKColor color,
                                    double? deltaMinutes = null, bool invertDelta = false)
    {
        double a = HourToAngle(time.Hour + time.Minute / 60.0);
        float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);

        float arcX = cx + R * cos;
        float arcY = cy - R * sin;

        // Icon or dot on the arc
        if (type is MarkerType.Rise or MarkerType.Set)
        {
            DrawSunIcon(c, arcX, arcY, R * 0.07f, color, isRise: type == MarkerType.Rise);
        }
        else
        {
            using var markPaint = new SKPaint
            {
                IsAntialias = true,
                Color = color,
                Style = SKPaintStyle.Fill
            };
            c.DrawCircle(arcX, arcY, 5f, markPaint);
        }

        // Time label just outside
        using var labelFont = new SKFont(SKTypeface.FromFamilyName("Arial"), R * 0.11f);
        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            Color = color
        };
        float lr = R * 1.22f;
        string label = type == MarkerType.Noon
            ? $"☀ {time:HH:mm}"
            : $"{time:HH:mm}";
        float lx = cx + lr * cos;
        float ly = cy - lr * sin + labelFont.Size * 0.35f;
        c.DrawText(label, lx, ly, SKTextAlign.Center, labelFont, textPaint);

        // Weekly delta text below the label
        if (deltaMinutes.HasValue)
        {
            double displayDelta = invertDelta ? -deltaMinutes.Value : deltaMinutes.Value;
            int weeklyDelta = (int)Math.Round(displayDelta * 7);
            if (weeklyDelta != 0)
            {
                string deltaStr = weeklyDelta > 0 ? $"+{weeklyDelta}m/wk" : $"{weeklyDelta}m/wk";
                var deltaColor = weeklyDelta > 0
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

    private static void DrawSunIcon(SKCanvas c, float x, float y, float size,
                                     SKColor color, bool isRise)
    {
        float sunR = size * 0.4f;

        using var fillPaint = new SKPaint
        {
            IsAntialias = true,
            Color = color,
            Style = SKPaintStyle.Fill
        };
        using var strokePaint = new SKPaint
        {
            IsAntialias = true,
            Color = color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            StrokeCap = SKStrokeCap.Round
        };

        // Horizon line
        c.DrawLine(x - size, y, x + size, y, strokePaint);

        // Half-sun (semicircle)
        using var sunPath = new SKPath();
        var sunRect = new SKRect(x - sunR, y - sunR, x + sunR, y + sunR);
        if (isRise)
            sunPath.AddArc(sunRect, 180f, 180f);  // top half
        else
            sunPath.AddArc(sunRect, 0f, 180f);    // bottom half
        c.DrawPath(sunPath, fillPaint);

        // Rays
        float rayInner = sunR * 1.5f;
        float rayOuter = sunR * 2.2f;
        for (int i = 0; i < 5; i++)
        {
            double angle = isRise
                ? Math.PI * (0.9 - i * 0.2)   // above horizon
                : Math.PI * (1.1 + i * 0.2);  // below horizon
            float rc = (float)Math.Cos(angle);
            float rs = (float)Math.Sin(angle);
            c.DrawLine(x + rayInner * rc, y - rayInner * rs,
                       x + rayOuter * rc, y - rayOuter * rs, strokePaint);
        }
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
                                        bool invertGain)
    {
        float yesterdayDeg = HourToSkiaDeg(yesterdayTime);
        float todayDeg = HourToSkiaDeg(todayTime);

        float diff = todayDeg - yesterdayDeg;
        if (diff > 180f) diff -= 360f;
        if (diff < -180f) diff += 360f;

        if (Math.Abs(diff) < 0.01f) return;

        bool gaining = invertGain ? diff < 0 : diff > 0;
        var color = gaining
            ? new SKColor(0x66, 0xBB, 0x6A, 0x50)  // green translucent
            : new SKColor(0xEF, 0x53, 0x50, 0x50);  // red translucent

        float startAngle = diff > 0 ? yesterdayDeg : todayDeg;
        float sweepAngle = Math.Abs(diff);

        var rect = new SKRect(cx - R, cy - R, cx + R, cy + R);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = color
        };
        using var path = new SKPath();
        path.MoveTo(cx, cy);
        path.ArcTo(rect, startAngle, sweepAngle, false);
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
