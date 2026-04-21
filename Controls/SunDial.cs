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

        // Steel bezel ring — unrotated, sits on top of straps, behind the dial fill.
        DrawBezel(canvas, cx, cy, R);

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
        DrawEventArcDots(canvas, cx, cy, R);   // only dots/sectors — rotate with dial
        DrawSun(canvas, cx, cy, R);

        canvas.Restore();

        // Fixed overlays — never rotate, always aligned with the rim.
        DrawRiseSetBoxes(canvas, cx, cy, R);
        DrawNoonLabel(canvas, cx, cy, R);
        DrawDateField(canvas, cx, cy, R);
        DrawLocationLabel(canvas, cx, cy, R);
        DrawBrandLogo(canvas, cx, cy, R);
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
        float halfW = R * 0.80f;
        float left = cx - halfW;
        float right = cx + halfW;
        float corner = R * 0.16f;
        float gap = R * 0.32f;   // how far the strap slides under the dial case

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
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(left, 0), new SKPoint(right, 0),
                bandGrad, bandPos, SKShaderTileMode.Clamp)
        };
        using var borderPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1f,
            Color = new SKColor(0x3A, 0x3A, 0x52)
        };

        var topRR = new SKRoundRect(new SKRect(left, topT, right, topB), corner);
        var botRR = new SKRoundRect(new SKRect(left, botT, right, botB), corner);
        c.DrawRoundRect(topRR, gradPaint); c.DrawRoundRect(topRR, borderPaint);
        c.DrawRoundRect(botRR, gradPaint); c.DrawRoundRect(botRR, borderPaint);

        // ── Edge stitching (dashed lines inset from each side) ──
        float sInset = halfW * 0.11f;
        using var stitchPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.7f,
            Color = new SKColor(0x52, 0x52, 0x70, 0x90),
            PathEffect = SKPathEffect.CreateDash(new[] { 3.5f, 2.5f }, 0f)
        };
        float sCornerOff = corner * 0.55f;
        // top band
        c.DrawLine(left + sInset, topT + sCornerOff, left + sInset, topB, stitchPaint);
        c.DrawLine(right - sInset, topT + sCornerOff, right - sInset, topB, stitchPaint);
        // bottom band
        c.DrawLine(left + sInset, botT, left + sInset, botB - sCornerOff, stitchPaint);
        c.DrawLine(right - sInset, botT, right - sInset, botB - sCornerOff, stitchPaint);

        // ── Horizontal texture ribs (rubber-band feel) ───────────
        float ribSpacing = R * 0.09f;
        float ribInset = corner * 0.40f;
        using var ribPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.5f,
            Color = new SKColor(0x08, 0x08, 0x14, 0x60)
        };
        // top band: ribs from dial edge upward
        for (float y = topB - ribSpacing; y > topT + corner; y -= ribSpacing)
            c.DrawLine(left + ribInset, y, right - ribInset, y, ribPaint);
        // bottom band: ribs from dial edge downward
        for (float y = botT + ribSpacing; y < botB - corner; y += ribSpacing)
            c.DrawLine(left + ribInset, y, right - ribInset, y, ribPaint);

        // ── Punch holes — bottom strap, centred vertically ───────
        float holeR = halfW * 0.038f;
        float holePitch = halfW * 0.20f;
        float holeCy = (botT + botB) * 0.5f;
        using var holeFill = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(0x08, 0x08, 0x14)
        };
        using var holeRim = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.7f,
            Color = new SKColor(0x42, 0x42, 0x5A)
        };
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
        float bkL = cx - bkW * 0.5f, bkR = cx + bkW * 0.5f;
        float bkT = bkCy - bkH * 0.5f, bkB = bkCy + bkH * 0.5f;
        using var buckleStroke = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.8f,
            Color = new SKColor(0x50, 0x50, 0x6A),
            StrokeCap = SKStrokeCap.Round
        };
        // Outer frame
        c.DrawRoundRect(new SKRect(bkL, bkT, bkR, bkB), bkH * 0.28f, bkH * 0.28f, buckleStroke);
        // Centre bar (divides frame in two)
        c.DrawLine(cx, bkT, cx, bkB, buckleStroke);
        // Pin (horizontal rod across one half, slightly past edges)
        using var pinStroke = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.4f,
            Color = new SKColor(0x60, 0x60, 0x80),
            StrokeCap = SKStrokeCap.Round
        };
        float pinY = (bkT + bkB) * 0.5f;
        c.DrawLine(bkL - R * 0.015f, pinY, cx, pinY, pinStroke);
    }

    // ── pilot-watch bezel ────────────────────────────────────────

    private static void DrawBezel(SKCanvas c, float cx, float cy, float R)
    {
        float innerR = R;
        float outerR = R * 1.20f;
        float bezelW = outerR - innerR;   // = R * 0.20

        // ── Steel annular fill: L→R gradient simulates circular brushing ──
        using var steelShader = SKShader.CreateLinearGradient(
            new SKPoint(cx - outerR, cy),
            new SKPoint(cx + outerR, cy),
            new SKColor[]
            {
                new(0x50, 0x50, 0x5C),
                new(0x80, 0x80, 0x90),
                new(0xB8, 0xB8, 0xC8),
                new(0xD0, 0xD0, 0xDC),
                new(0xA8, 0xA8, 0xB8),
                new(0x78, 0x78, 0x88),
                new(0x98, 0x98, 0xA8),
                new(0x50, 0x50, 0x5C),
            },
            new float[] { 0f, 0.12f, 0.30f, 0.50f, 0.65f, 0.76f, 0.88f, 1f },
            SKShaderTileMode.Clamp);

        using var steelPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = steelShader
        };
        using var annPath = new SKPath();
        annPath.AddCircle(cx, cy, outerR);
        annPath.AddCircle(cx, cy, innerR);
        annPath.FillType = SKPathFillType.EvenOdd;
        c.DrawPath(annPath, steelPaint);

        // ── Bevel: highlight top-left, shadow bottom-right ───
        var outerRect = new SKRect(cx - outerR, cy - outerR, cx + outerR, cy + outerR);
        using var hiPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Color = new SKColor(0xFF, 0xFF, 0xFF, 0x55)
        };
        using var shPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Color = new SKColor(0x00, 0x00, 0x00, 0x55)
        };
        c.DrawArc(outerRect, 195f, 165f, false, hiPaint);   // top-left arc
        c.DrawArc(outerRect, 15f, 165f, false, shPaint);   // bottom-right arc

        // Inner shadow edge flush with dial
        using var innerEdgePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Color = new SKColor(0x18, 0x18, 0x24, 0xD0)
        };
        c.DrawCircle(cx, cy, innerR, innerEdgePaint);

        // ── Tick marks: 60 positions, major every 5 ──────────
        using var majTickPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Color = new SKColor(0xE4, 0xE4, 0xF0)
        };
        using var minTickPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.8f,
            Color = new SKColor(0xA8, 0xA8, 0xB8, 0xCC)
        };

        for (int i = 0; i < 60; i++)
        {
            // 0 at top, clockwise — standard watch convention
            double angle = Math.PI / 2.0 - i * (2.0 * Math.PI / 60.0);
            float cos = (float)Math.Cos(angle);
            float sin = (float)Math.Sin(angle);

            bool major = i % 5 == 0;
            float tickLen = major ? bezelW * 0.36f : bezelW * 0.20f;
            float tickInner = outerR - tickLen;

            c.DrawLine(
                cx + tickInner * cos, cy - tickInner * sin,
                cx + (outerR - 1f) * cos, cy - (outerR - 1f) * sin,
                major ? majTickPaint : minTickPaint);
        }

        // ── Rectangular index batons at 3, 6, 9 o'clock ─────
        float batonW = bezelW * 0.28f;   // width of baton (tangential)
        float batonH = bezelW * 0.44f;   // depth of baton (radial)
        float batonOuter = outerR - bezelW * 0.10f;
        float batonInner = batonOuter - batonH;
        using var batonPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(0xF0, 0xF0, 0xF8)
        };

        // Cardinal positions: 15 min = right (3 o'clock), 30 = bottom (6), 45 = left (9)
        foreach (int min in new[] { 15, 30, 45 })
        {
            double angle = Math.PI / 2.0 - min * (2.0 * Math.PI / 60.0);
            float cos = (float)Math.Cos(angle);
            float sin = (float)Math.Sin(angle);

            // Centre of the baton
            float bCx = cx + ((batonOuter + batonInner) / 2f) * cos;
            float bCy = cy - ((batonOuter + batonInner) / 2f) * sin;

            c.Save();
            c.Translate(bCx, bCy);
            c.RotateDegrees(-(float)(angle * 180.0 / Math.PI));  // align radially
            c.DrawRect(-batonW / 2f, -batonH / 2f, batonW, batonH, batonPaint);
            c.Restore();
        }

        // ── Triangle pointer at 12 o'clock ────────────────────
        float triTip = outerR - bezelW * 0.07f;
        float triBase = outerR - bezelW * 0.52f;
        float triHalfW = bezelW * 0.20f;
        using var triPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(0xF0, 0xF0, 0xF8)
        };
        using var triPath = new SKPath();
        triPath.MoveTo(cx, cy - triTip);
        triPath.LineTo(cx - triHalfW, cy - triBase);
        triPath.LineTo(cx + triHalfW, cy - triBase);
        triPath.Close();
        c.DrawPath(triPath, triPaint);
    }

    // ── location label (inside the clock, upper sky area) ───────

    private void DrawLocationLabel(SKCanvas c, float cx, float cy, float R)
    {
        if (string.IsNullOrEmpty(LocationLabel)) return;

        using var font = new SKFont(SKTypeface.FromFamilyName("Arial"), R * 0.065f);
        using var paint = new SKPaint { IsAntialias = true, Color = new SKColor(0xFF, 0xFF, 0xFF, 0x60) };
        float y = cy - R * 0.808f + font.Size * 0.35f;
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
        c.DrawCircle(cx, cy, 1.5f, dotPaint);
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

    /// <summary>Draws diff sectors and arc dots only — rotates with the dial.</summary>
    private void DrawEventArcDots(SKCanvas c, float cx, float cy, float R)
    {
        if (_sun is null) return;

        if (_yesterdaySun is not null && SettingsService.ShowWeekDiffs)
        {
            DrawDiffSector(c, cx, cy, R, _yesterdaySun.Sunrise, _sun.Sunrise, invertGain: true);
            DrawDiffSector(c, cx, cy, R, _yesterdaySun.Sunset, _sun.Sunset, invertGain: false);
        }

        // Rise dot
        DrawArcDot(c, cx, cy, R, _sun.Sunrise, new SKColor(0xFF, 0xA5, 0x00));
        // Set dot
        DrawArcDot(c, cx, cy, R, _sun.Sunset, new SKColor(0xFF, 0x63, 0x47));
        // Noon dot (always shown; suppressed only when NoonAtTop)
        if (!SettingsService.NoonAtTop)
            DrawArcDot(c, cx, cy, R, _sun.SolarNoon, new SKColor(0xFF, 0xD7, 0x00));
    }

    private static void DrawArcDot(SKCanvas c, float cx, float cy, float R,
                                    DateTime time, SKColor color)
    {
        double a = HourToAngle(time.Hour + time.Minute / 60.0);
        float x = cx + R * (float)Math.Cos(a);
        float y = cy - R * (float)Math.Sin(a);
        using var p = new SKPaint { IsAntialias = true, Color = color, Style = SKPaintStyle.Fill };
        c.DrawCircle(x, y, 5f, p);
    }

    /// <summary>Fixed rise + set label boxes — drawn outside rotation, always upright.</summary>
    private void DrawRiseSetBoxes(SKCanvas c, float cx, float cy, float R)
    {
        if (_sun is null) return;

        double? riseDelta = null, setDelta = null;
        if (_yesterdaySun is not null)
        {
            riseDelta = (_sun.Sunrise.TimeOfDay - _yesterdaySun.Sunrise.TimeOfDay).TotalMinutes;
            setDelta = (_sun.Sunset.TimeOfDay - _yesterdaySun.Sunset.TimeOfDay).TotalMinutes;
        }
        double? visibleRiseDelta = SettingsService.ShowWeekDiffs ? riseDelta : null;
        double? visibleSetDelta = SettingsService.ShowWeekDiffs ? setDelta : null;

        DrawRiseSetField(c, cx, cy, R, _sun.Sunrise, MarkerType.Rise,
                         new SKColor(0xFF, 0xA5, 0x00), visibleRiseDelta, invertDelta: true);
        DrawRiseSetField(c, cx, cy, R, _sun.Sunset, MarkerType.Set,
                         new SKColor(0xFF, 0x63, 0x47), visibleSetDelta, invertDelta: false);
    }

    /// <summary>Fixed noon time label — drawn outside rotation, always upright.</summary>
    private void DrawNoonLabel(SKCanvas c, float cx, float cy, float R)
    {
        if (_sun is null || SettingsService.NoonAtTop || !SettingsService.ShowApexTime) return;

        var color = new SKColor(0xFF, 0xD7, 0x00);
        float fontSize = R * 0.085f;
        float rowY = cy - R * 0.65f;
        using var font = new SKFont(SKTypeface.FromFamilyName("Arial"), fontSize);
        using var paint = new SKPaint { IsAntialias = true, Color = color };
        c.DrawText($"{_sun.SolarNoon:HH:mm}", cx, rowY + fontSize * 0.35f,
                   SKTextAlign.Center, font, paint);
    }

    /// <summary>
    /// Tight instrument field at a fixed row aligned with the "12" label.
    /// Rise sits left, Set sits right. Always upright — call outside rotation.
    /// </summary>
    private static void DrawRiseSetField(SKCanvas c, float cx, float cy, float R,
                                          DateTime time, MarkerType type, SKColor color,
                                          double? deltaMinutes, bool invertDelta)
    {
        bool hasDelta = deltaMinutes.HasValue &&
                        (int)Math.Round(Math.Abs(deltaMinutes.Value) * 7) != 0;

        float fontSize = R * 0.078f;
        float deltaFontSize = R * 0.065f;
        float padY = R * 0.030f;
        float lineGap = R * 0.018f;
        float corner = R * 0.028f;

        float boxH = padY * 2 + fontSize + (hasDelta ? lineGap + deltaFontSize : 0f);
        float boxW = R * 0.26f;

        // Fixed row at the "12" label height (nr = R*0.58*1.12 = R*0.65 above centre)
        float rowY = cy - R * 0.65f;
        float fieldCx = type == MarkerType.Rise ? cx - R * 0.44f : cx + R * 0.44f;

        float boxLeft = fieldCx - boxW / 2f;
        float boxTop = rowY - boxH / 2f;
        var rect = new SKRect(boxLeft, boxTop, boxLeft + boxW, boxTop + boxH);

        // ── Background ───────────────────────────────────────
        using var bgPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(0x18, 0x18, 0x28, 0xCC)
        };
        c.DrawRoundRect(rect, corner, corner, bgPaint);

        // ── Border: steel-style with highlight/shadow bevel ──
        using var borderPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.1f,
            Color = new SKColor(0xB0, 0xB0, 0xC4, 0xCC)
        };
        c.DrawRoundRect(rect, corner, corner, borderPaint);

        // Highlight top-left edge, shadow bottom-right
        using var hiPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.7f,
            Color = new SKColor(0xFF, 0xFF, 0xFF, 0x40)
        };
        using var shPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.7f,
            Color = new SKColor(0x00, 0x00, 0x00, 0x50)
        };
        // Top + left highlight
        c.DrawLine(rect.Left + corner, rect.Top, rect.Right - corner, rect.Top, hiPaint);
        c.DrawLine(rect.Left, rect.Top + corner, rect.Left, rect.Bottom - corner, hiPaint);
        // Bottom + right shadow
        c.DrawLine(rect.Left + corner, rect.Bottom, rect.Right - corner, rect.Bottom, shPaint);
        c.DrawLine(rect.Right, rect.Top + corner, rect.Right, rect.Bottom - corner, shPaint);

        // ── Time text — vertically centred in box ────────────
        float boxCy = rowY;
        float timeY = hasDelta
            ? boxTop + padY + fontSize * 0.78f          // nudge up when delta row below
            : boxCy + fontSize * 0.35f;                 // true vertical centre
        using var timeFont = new SKFont(SKTypeface.FromFamilyName("Arial"), fontSize);
        using var timePaint = new SKPaint { IsAntialias = true, Color = color };
        c.DrawText($"{time:HH:mm}", fieldCx, timeY, SKTextAlign.Center, timeFont, timePaint);

        // ── Delta row ────────────────────────────────────────
        if (hasDelta)
        {
            double displayDelta = invertDelta ? -deltaMinutes!.Value : deltaMinutes!.Value;
            int weeklyDelta = (int)Math.Round(displayDelta * 7);
            string deltaStr = weeklyDelta > 0 ? $"+{weeklyDelta}m/wk" : $"{weeklyDelta}m/wk";
            var deltaColor = weeklyDelta > 0
                ? new SKColor(0x66, 0xBB, 0x6A)
                : new SKColor(0xEF, 0x53, 0x50);
            using var deltaFont = new SKFont(SKTypeface.FromFamilyName("Arial"), deltaFontSize);
            using var deltaPaint = new SKPaint { IsAntialias = true, Color = deltaColor };
            c.DrawText(deltaStr, fieldCx, timeY + lineGap + deltaFontSize * 0.78f,
                       SKTextAlign.Center, deltaFont, deltaPaint);
        }
    }

    /// <summary>Instrument field showing the current date, centred in the upper sky area.</summary>
    private void DrawDateField(SKCanvas c, float cx, float cy, float R)
    {
        string dateStr = _localNow.ToString("yyyy-MM-dd");

        float fontSize = R * 0.078f;
        float padY = R * 0.030f;
        float corner = R * 0.028f;
        float boxW = R * 0.52f;
        float boxH = padY * 2 + fontSize;

        // Between the "0" label (cy + R*0.65) and the inner rim (cy + R) → midpoint ~cy + R*0.82
        float boxCx = cx;
        float boxCy = cy + R * 0.82f;
        var rect = new SKRect(boxCx - boxW / 2f, boxCy - boxH / 2f,
                              boxCx + boxW / 2f, boxCy + boxH / 2f);

        using var bgPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(0x18, 0x18, 0x28, 0xCC)
        };
        c.DrawRoundRect(rect, corner, corner, bgPaint);

        using var borderPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.1f,
            Color = new SKColor(0xB0, 0xB0, 0xC4, 0xCC)
        };
        c.DrawRoundRect(rect, corner, corner, borderPaint);

        using var hiPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.7f,
            Color = new SKColor(0xFF, 0xFF, 0xFF, 0x40)
        };
        using var shPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.7f,
            Color = new SKColor(0x00, 0x00, 0x00, 0x50)
        };
        c.DrawLine(rect.Left + corner, rect.Top, rect.Right - corner, rect.Top, hiPaint);
        c.DrawLine(rect.Left, rect.Top + corner, rect.Left, rect.Bottom - corner, hiPaint);
        c.DrawLine(rect.Left + corner, rect.Bottom, rect.Right - corner, rect.Bottom, shPaint);
        c.DrawLine(rect.Right, rect.Top + corner, rect.Right, rect.Bottom - corner, shPaint);

        using var font = new SKFont(SKTypeface.FromFamilyName("Arial"), fontSize);
        using var paint = new SKPaint { IsAntialias = true, Color = new SKColor(0xEC, 0xEC, 0xF4) };
        c.DrawText(dateStr, boxCx, boxCy + fontSize * 0.35f, SKTextAlign.Center, font, paint);
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

    // ── brand logo badge ────────────────────────────────────

    /// <summary>
    /// Small fixed badge in the upper sky area (between dial centre and 12 o'clock).
    /// Black cat silhouette inside a gold ring — "BLACK CAT STUDIO" in tiny lettering below.
    /// </summary>
    private static void DrawBrandLogo(SKCanvas c, float cx, float cy, float R)
    {
        float logoCx = cx;
        float logoCy = cy - R * 0.07f;   // upper sky, above dial centre
        float logoR = R * 0.115f;       // ring radius — deliberately small

        // ── Cat silhouette ─────────────────────────────────────
        // Original path is in 200×200 SVG space, centre ≈ (100,100).
        // Farthest corner is ~118.7 units from centre; using /120 leaves a small margin.
        float s = logoR / 120f;

        float[] pts =
        {
            22.7f,25.9f, 24.6f,36.8f, 14.1f,43.5f, 11.9f,49.7f, 8.1f,55.7f,
            10.3f,61.9f, 12.7f,63.5f, 21.9f,64.1f, 24.9f,66.8f, 24.9f,72.7f,
            23.0f,83.2f, 23.2f,90.0f, 28.4f,102.4f, 35.9f,113.0f, 37.6f,117.6f,
            39.7f,134.9f, 39.7f,150.3f, 38.1f,158.1f, 31.9f,162.4f, 31.4f,165.7f,
            33.0f,168.4f, 40.0f,169.7f, 45.4f,167.0f, 55.4f,132.7f, 57.6f,136.2f,
            60.0f,145.9f, 67.3f,157.6f, 67.3f,158.6f, 63.5f,160.8f, 60.0f,161.1f,
            57.3f,163.2f, 56.5f,167.3f, 58.9f,169.5f, 103.8f,170.3f, 107.6f,169.5f,
            111.9f,166.5f, 124.1f,169.5f, 160.0f,169.7f, 170.8f,170.8f, 185.9f,174.1f,
            191.4f,174.1f, 191.1f,171.6f, 188.1f,169.5f, 174.9f,164.9f, 154.6f,162.2f,
            127.6f,160.8f, 119.2f,157.8f, 116.2f,154.3f, 115.4f,151.1f, 117.0f,141.1f,
            117.0f,130.8f, 115.7f,123.0f, 110.5f,106.5f, 103.2f,93.0f, 95.1f,84.1f,
            88.9f,79.7f, 65.7f,71.1f, 58.9f,62.7f, 50.3f,56.5f, 41.1f,40.8f, 23.5f,25.4f
        };

        using var catPath = new SKPath();
        catPath.MoveTo(logoCx + (pts[0] - 100f) * s, logoCy + (pts[1] - 100f) * s);
        for (int i = 2; i < pts.Length; i += 2)
            catPath.LineTo(logoCx + (pts[i] - 100f) * s, logoCy + (pts[i + 1] - 100f) * s);
        catPath.Close();

        using var catPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(0x08, 0x08, 0x14, 0xF2)
        };
        c.DrawPath(catPath, catPaint);

        // ── Gold outer ring ────────────────────────────────────
        float ringW = Math.Max(0.8f, logoR * 0.055f);
        using var ringPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = ringW,
            Color = new SKColor(0xC8, 0xA8, 0x4A, 0xBB)
        };
        c.DrawCircle(logoCx, logoCy, logoR + ringW * 0.5f, ringPaint);

        // Gold inner hairline
        using var innerRing = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(0.4f, logoR * 0.022f),
            Color = new SKColor(0xC8, 0xA8, 0x4A, 0x55)
        };
        c.DrawCircle(logoCx, logoCy, logoR * 0.90f, innerRing);

        // ── Notional diameter line — positions the text band, not drawn ──
        float divY = logoCy + logoR * 0.58f;

        // ── "BLACK · CAT · STUDIO" — Black left of rim, Cat centred, Studio right ──
        float textSize = Math.Max(5f, logoR * 0.24f);
        float textY    = divY + (logoCy + logoR - divY) * 0.50f + textSize * 0.35f;
        float rimGap   = logoR * 0.06f;   // small breathing space between words and ring edge

        using var textFont = new SKFont(
            SKTypeface.FromFamilyName("Georgia", SKFontStyleWeight.Bold,
                SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), textSize);
        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.StrokeAndFill,
            StrokeWidth = textSize * 0.12f,
            Color = new SKColor(0xA8, 0x84, 0x28, 0xCC)
        };

        float catY = divY + (logoCy + logoR - divY) * 0.5f + textSize * 0.35f;   // between cat and lower rim

        // "BLACK" — right-aligned, ending at the left rim
        c.DrawText("BLACK",  logoCx - logoR, catY, SKTextAlign.Right,  textFont, textPaint);
        // "CAT"   — centred below the ring
        c.DrawText("CAT",    logoCx,         catY, SKTextAlign.Center,  textFont, textPaint);
        // "STUDIO"— left-aligned, starting from the right rim
        c.DrawText("STUDIO", logoCx + logoR, catY, SKTextAlign.Left,   textFont, textPaint);
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
