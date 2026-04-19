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

    /// <summary>Location string drawn in the bottom strap (e.g. "59.33°N 18.07°E").</summary>
    public string LocationLabel { get; set; } = "";

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

        // Straps are drawn first (unrotated, behind everything).
        DrawStraps(canvas, cx, cy, R, info.Height);

        // When NoonAtTop is on, rotate the canvas so solar noon sits at 12 o'clock.
        // HourToAngle(noon) gives the math-convention angle; the top of the clock is π/2.
        // The canvas rotation (clockwise degrees) is the negated difference.
        canvas.Save();
        if (SettingsService.NoonAtTop)
        {
            double noonAngle = HourToAngle(_sun.SolarNoon.Hour + _sun.SolarNoon.Minute / 60.0
                                           + _sun.SolarNoon.Second / 3600.0);
            double rotRad = Math.PI / 2.0 - noonAngle;
            canvas.RotateDegrees((float)(-rotRad * 180.0 / Math.PI), cx, cy);
        }

        DrawSkyGround(canvas, cx, cy, R, _sun);
        DrawHorizonLine(canvas, cx, cy, R);
        DrawClockFace(canvas, cx, cy, R * 0.58f);
        if (SettingsService.ShowDurations)
            DrawDurationLabels(canvas, cx, cy, R, _sun);
        DrawEventMarkers(canvas, cx, cy, R);
        DrawSun(canvas, cx, cy, R);

        canvas.Restore();

        // Location label on top of everything, unrotated.
        DrawLocationLabel(canvas, cx, cy, R);
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

    // ── watch straps ─────────────────────────────────────────

    private static void DrawStraps(SKCanvas c, float cx, float cy, float R, float canvasH)
    {
        float halfW  = R * 0.80f;
        float left   = cx - halfW;
        float right  = cx + halfW;
        float corner = R * 0.16f;
        float gap    = R * 0.32f;   // how far the strap slides under the dial case

        float topT = 0f;
        float topB = cy - R + gap;
        float botT = cy + R - gap;
        float botB = canvasH;

        // ── 3-D raised gradient: dark edges → lighter centre ──
        var bandGrad = new SKColor[]
        {
            new(0x10, 0x10, 0x1E),
            new(0x1C, 0x1C, 0x2C),
            new(0x28, 0x28, 0x3C),
            new(0x1C, 0x1C, 0x2C),
            new(0x10, 0x10, 0x1E),
        };
        var bandPos = new float[] { 0f, 0.12f, 0.50f, 0.88f, 1f };

        using var gradPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(left, 0), new SKPoint(right, 0),
                bandGrad, bandPos, SKShaderTileMode.Clamp)
        };
        using var borderPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f, Color = new SKColor(0x3A, 0x3A, 0x52)
        };

        var topRR = new SKRoundRect(new SKRect(left, topT, right, topB), corner);
        var botRR = new SKRoundRect(new SKRect(left, botT, right, botB), corner);
        c.DrawRoundRect(topRR, gradPaint);  c.DrawRoundRect(topRR, borderPaint);
        c.DrawRoundRect(botRR, gradPaint);  c.DrawRoundRect(botRR, borderPaint);

        // ── Edge stitching (dashed lines inset from each side) ──
        float sInset = halfW * 0.11f;
        using var stitchPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.7f,
            Color = new SKColor(0x52, 0x52, 0x70, 0x90),
            PathEffect = SKPathEffect.CreateDash(new[] { 3.5f, 2.5f }, 0f)
        };
        float sCornerOff = corner * 0.55f;
        // top band
        c.DrawLine(left  + sInset, topT + sCornerOff, left  + sInset, topB, stitchPaint);
        c.DrawLine(right - sInset, topT + sCornerOff, right - sInset, topB, stitchPaint);
        // bottom band
        c.DrawLine(left  + sInset, botT, left  + sInset, botB - sCornerOff, stitchPaint);
        c.DrawLine(right - sInset, botT, right - sInset, botB - sCornerOff, stitchPaint);

        // ── Horizontal texture ribs (rubber-band feel) ───────────
        float ribSpacing = R * 0.09f;
        float ribInset   = corner * 0.40f;
        using var ribPaint = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.5f, Color = new SKColor(0x08, 0x08, 0x14, 0x60)
        };
        // top band: ribs from dial edge upward
        for (float y = topB - ribSpacing; y > topT + corner; y -= ribSpacing)
            c.DrawLine(left + ribInset, y, right - ribInset, y, ribPaint);
        // bottom band: ribs from dial edge downward
        for (float y = botT + ribSpacing; y < botB - corner; y += ribSpacing)
            c.DrawLine(left + ribInset, y, right - ribInset, y, ribPaint);

        // ── Punch holes — bottom strap, centred vertically ───────
        float holeR      = halfW * 0.038f;
        float holePitch  = halfW * 0.20f;
        float holeCy     = (botT + botB) * 0.5f;
        using var holeFill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill,
            Color = new SKColor(0x08, 0x08, 0x14) };
        using var holeRim = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.7f, Color = new SKColor(0x42, 0x42, 0x5A) };
        for (int i = -2; i <= 2; i++)
        {
            float hx = cx + i * holePitch;
            c.DrawCircle(hx, holeCy, holeR, holeFill);
            c.DrawCircle(hx, holeCy, holeR, holeRim);
        }

        // ── Pin buckle — top strap, 1/3 of the way from the outer end ──
        float bkW = halfW * 0.52f;
        float bkH = R * 0.090f;
        float bkCy = topT + (topB - topT) * 0.33f;
        float bkL  = cx - bkW * 0.5f,  bkR = cx + bkW * 0.5f;
        float bkT  = bkCy - bkH * 0.5f, bkB = bkCy + bkH * 0.5f;
        using var buckleStroke = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.8f, Color = new SKColor(0x50, 0x50, 0x6A),
            StrokeCap = SKStrokeCap.Round
        };
        // Outer frame
        c.DrawRoundRect(new SKRect(bkL, bkT, bkR, bkB), bkH * 0.28f, bkH * 0.28f, buckleStroke);
        // Centre bar (divides frame in two)
        c.DrawLine(cx, bkT, cx, bkB, buckleStroke);
        // Pin (horizontal rod across one half, slightly past edges)
        using var pinStroke = new SKPaint
        {
            IsAntialias = true, Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.4f, Color = new SKColor(0x60, 0x60, 0x80),
            StrokeCap = SKStrokeCap.Round
        };
        float pinY = (bkT + bkB) * 0.5f;
        c.DrawLine(bkL - R * 0.015f, pinY, cx, pinY, pinStroke);
    }

    // ── location label (inside the clock, upper sky area) ───────

    private void DrawLocationLabel(SKCanvas c, float cx, float cy, float R)
    {
        if (string.IsNullOrEmpty(LocationLabel)) return;

        using var font  = new SKFont(SKTypeface.FromFamilyName("Arial"), R * 0.082f);
        using var paint = new SKPaint { IsAntialias = true, Color = new SKColor(0xFF, 0xFF, 0xFF, 0x60) };
        // Inside the circle, just below the 12-o'clock rim — right under noon.
        float y = cy - R * 0.78f + font.Size * 0.35f;
        c.DrawText(LocationLabel, cx, y, SKTextAlign.Center, font, paint);
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
                float nr = r * 1.12f;
                float tx = cx + nr * cos;
                float ty = cy - nr * sin + numFont.Size * 0.35f;
                c.DrawText(h.ToString(), tx, ty, SKTextAlign.Center, numFont, numPaint);
            }
        }

        // Minute arm (ticks discretely on every minute, no lerp).
        // Standard 60-minute sweep: 0 min → top, 15 → right, 30 → bottom, 45 → left.
        double minFraction = _localNow.Minute / 60.0;
        double minAngle = Math.PI / 2.0 - 2.0 * Math.PI * minFraction;
        using var minPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            StrokeCap = SKStrokeCap.Round,
            Color = new SKColor(0xFF, 0xFF, 0xFF, 0xBB)
        };
        float mLen = r * 0.72f;
        c.DrawLine(cx, cy,
                   cx + mLen * (float)Math.Cos(minAngle),
                   cy - mLen * (float)Math.Sin(minAngle), minPaint);

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

        // Second hand. `_animatedLocalNow` is minute-quantized between Refreshes, so drive
        // this from real wall-clock time so it keeps ticking every animation frame.
        var liveNow = DateTime.Now;
        double secFraction = (liveNow.Second + liveNow.Millisecond / 1000.0) / 60.0;
        double secAngle = Math.PI / 2.0 - 2.0 * Math.PI * secFraction;
        using var secPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            StrokeCap = SKStrokeCap.Round,
            Color = new SKColor(0xEF, 0x53, 0x50, 0xCC) // soft red
        };
        float sLen = r * 0.80f;
        c.DrawLine(cx, cy,
                   cx + sLen * (float)Math.Cos(secAngle),
                   cy - sLen * (float)Math.Sin(secAngle), secPaint);

        // Center dot
        using var dotPaint = new SKPaint { IsAntialias = true, Color = SKColors.White };
        c.DrawCircle(cx, cy, 3f, dotPaint);
    }

    // ── daytime / nighttime duration numbers ───────────────

    private static void DrawDurationLabels(SKCanvas c, float cx, float cy, float R,
                                            SolarCalculator.SunData sun)
    {
        var daytime = sun.Sunset - sun.Sunrise;
        var nighttime = TimeSpan.FromHours(24) - daytime;

        string dayText = $"{(int)daytime.TotalHours}h {daytime.Minutes}m";
        string nightText = $"{(int)nighttime.TotalHours}h {nighttime.Minutes}m";

        using var font = new SKFont(SKTypeface.FromFamilyName("Arial"), R * 0.10f);

        using var dayPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0x66, 0xBB, 0x6A)
        };
        using var nightPaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0xEF, 0x53, 0x50)
        };

        c.DrawText(dayText, cx, cy - R * 0.22f, SKTextAlign.Center, font, dayPaint);
        c.DrawText(nightText, cx, cy + R * 0.22f, SKTextAlign.Center, font, nightPaint);
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
        if (_yesterdaySun is not null && SettingsService.ShowWeekDiffs)
        {
            DrawDiffSector(c, cx, cy, R, _yesterdaySun.Sunrise, _sun.Sunrise, invertGain: true);
            DrawDiffSector(c, cx, cy, R, _yesterdaySun.Sunset, _sun.Sunset, invertGain: false);
        }

        // Pass deltas only when week diffs are enabled so the ±m/wk text is also hidden.
        double? visibleRiseDelta = SettingsService.ShowWeekDiffs ? riseDelta : null;
        double? visibleSetDelta  = SettingsService.ShowWeekDiffs ? setDelta  : null;

        DrawMarker(c, cx, cy, R, _sun.Sunrise, MarkerType.Rise, new SKColor(0xFF, 0xA5, 0x00), visibleRiseDelta, invertDelta: true);
        DrawMarker(c, cx, cy, R, _sun.Sunset, MarkerType.Set, new SKColor(0xFF, 0x63, 0x47), visibleSetDelta, invertDelta: false);
        // When NoonAtTop is on the top of the dial IS solar noon — marker is redundant.
        if (!SettingsService.NoonAtTop)
            DrawMarker(c, cx, cy, R, _sun.SolarNoon, MarkerType.Noon, new SKColor(0xFF, 0xD7, 0x00),
                       showLabel: SettingsService.ShowApexTime);
    }

    private static void DrawMarker(SKCanvas c, float cx, float cy, float R,
                                    DateTime time, MarkerType type, SKColor color,
                                    double? deltaMinutes = null, bool invertDelta = false,
                                    bool showLabel = true)
    {
        double a = HourToAngle(time.Hour + time.Minute / 60.0);
        float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);

        float arcX = cx + R * cos;
        float arcY = cy - R * sin;

        // Icon or dot on the arc
        if (type is MarkerType.Rise or MarkerType.Set)
        {
            // Half-sun icons temporarily disabled while iterating on styling.
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

        if (!showLabel) return;

        // Time label: Rise / Noon / Set are pinned to fixed upper-quadrant angles so
        // they always sit in the sky half, inside the outer ring.
        using var labelFont = new SKFont(SKTypeface.FromFamilyName("Arial"), R * 0.10f);
        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            Color = color
        };
        double labelAngleRad = type switch
        {
            MarkerType.Rise => Math.PI * 2.0 / 3.0,  // 120° (upper-left)
            MarkerType.Set  => Math.PI / 3.0,        //  60° (upper-right)
            _               => Math.PI / 2.0,       //  90° (straight up)
        };
        float labelLr = R * 0.80f;  // inward so the sun can pass between labels and ring
        float lcos = (float)Math.Cos(labelAngleRad);
        float lsin = (float)Math.Sin(labelAngleRad);
        float lx = cx + labelLr * lcos;
        float ly = cy - labelLr * lsin + labelFont.Size * 0.35f;
        string label = $"{time:HH:mm}";
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

                using var deltaFont = new SKFont(SKTypeface.FromFamilyName("Arial"), R * 0.09f);
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

        var nowTod = _animatedLocalNow.TimeOfDay;
        bool aboveHorizon = nowTod >= _sun.Sunrise.TimeOfDay && nowTod < _sun.Sunset.TimeOfDay;
        float sunRadius = aboveHorizon ? R * 0.07f : R * 0.045f;
        byte alpha = aboveHorizon ? (byte)0xFF : (byte)0x45;

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

        // Altitude label — placed just inside the sun along the same radius, upright.
        if (SettingsService.ShowSunAngle)
        {
            using var altFont = new SKFont(SKTypeface.FromFamilyName("Arial"), R * 0.08f);
            using var altPaint = new SKPaint
            {
                IsAntialias = true,
                Color = new SKColor(0xFF, 0xFF, 0xFF, 0xCC)
            };
            float altR = R * 0.88f;
            string altText = $"{_sun.Altitude:F1}°";
            float ax = cx + altR * cos;
            float ay = cy - altR * sin + altFont.Size * 0.35f;
            c.DrawText(altText, ax, ay, SKTextAlign.Center, altFont, altPaint);
        }
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
