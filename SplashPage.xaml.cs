using System;
using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace SunTime;

public sealed partial class SplashPage : Page
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private DispatcherTimer? _animTimer;

    private const double RiseDurationSec = 8.0;
    private const double HoldSec = 1.5;
    private bool _navigated;

    public SplashPage()
    {
        this.InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animTimer.Tick += (_, _) => SplashCanvas.Invalidate();
        _animTimer.Start();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;
        canvas.Clear(SKColors.Black);

        float w = info.Width;
        float h = info.Height;
        float cx = w / 2f;
        float horizonY = h * 0.55f;
        float sunRadius = Math.Min(w, h) * 0.12f;

        double elapsed = _clock.Elapsed.TotalSeconds;
        double t = Math.Clamp(elapsed / RiseDurationSec, 0, 1);

        // Ease-out cubic — slow start, decelerates into final position
        double eased = 1.0 - Math.Pow(1.0 - t, 3);

        // Sun travels from 7 radii below horizon to 2.5 radii above
        float sunCenterY = horizonY + sunRadius * 7.0f
                           - (float)(eased * sunRadius * 9.5f);

        // ── Sky gradient — animates through night → twilight → dawn → sunrise ──
        // Colour keyframes: (eased-value, r, g, b)
        static SKColor SkyColor(double p, (double at, int r, int g, int b)[] keys)
        {
            p = Math.Clamp(p, 0.0, 1.0);
            for (int i = 1; i < keys.Length; i++)
            {
                if (p <= keys[i].at)
                {
                    double f = (p - keys[i - 1].at) / (keys[i].at - keys[i - 1].at);
                    return new SKColor(
                        (byte)(keys[i - 1].r + f * (keys[i].r - keys[i - 1].r)),
                        (byte)(keys[i - 1].g + f * (keys[i].g - keys[i - 1].g)),
                        (byte)(keys[i - 1].b + f * (keys[i].b - keys[i - 1].b)));
                }
            }
            var k = keys[^1];
            return new SKColor((byte)k.r, (byte)k.g, (byte)k.b);
        }

        // Zenith: near-black → deep midnight blue → rich sky blue
        var zenith = SkyColor(eased, new[]
        {
            (0.00, 0x06, 0x06, 0x12),
            (0.30, 0x08, 0x10, 0x38),
            (0.60, 0x0E, 0x28, 0x62),
            (1.00, 0x18, 0x4A, 0x90),
        });

        // Mid-sky: dark navy → deep indigo → blue-violet → steel blue
        var midSky = SkyColor(eased, new[]
        {
            (0.00, 0x0A, 0x0A, 0x1C),
            (0.25, 0x16, 0x10, 0x44),
            (0.50, 0x28, 0x20, 0x70),
            (0.75, 0x36, 0x48, 0x90),
            (1.00, 0x50, 0x7A, 0xB8),
        });

        // Near-horizon: dark → deep purple → rose-mauve → warm amber
        var lowerSky = SkyColor(eased, new[]
        {
            (0.00, 0x10, 0x0C, 0x22),
            (0.20, 0x30, 0x14, 0x50),
            (0.40, 0x70, 0x28, 0x68),
            (0.60, 0xB8, 0x50, 0x48),
            (0.80, 0xF0, 0x90, 0x28),
            (1.00, 0xFF, 0xC8, 0x60),
        });

        // Horizon edge: darkest start → warm purple → coral → golden
        var horizon = SkyColor(eased, new[]
        {
            (0.00, 0x12, 0x0A, 0x20),
            (0.15, 0x3A, 0x16, 0x48),
            (0.35, 0x88, 0x30, 0x60),
            (0.55, 0xCC, 0x58, 0x30),
            (0.75, 0xFF, 0xA0, 0x20),
            (1.00, 0xFF, 0xD8, 0x70),
        });

        using var skyPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(cx, 0),
                new SKPoint(cx, horizonY),
                new[] { zenith, midSky, lowerSky, horizon },
                new[] { 0f, 0.40f, 0.75f, 1.0f },
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(0, 0, w, horizonY, skyPaint);

        // Ground below horizon stays very dark, warms very slightly
        var groundTop = SkyColor(eased, new[]
        {
            (0.00, 0x08, 0x08, 0x10),
            (1.00, 0x18, 0x12, 0x14),
        });
        using var groundPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(cx, horizonY),
                new SKPoint(cx, h),
                new[] { groundTop, new SKColor(0x04, 0x04, 0x08) },
                null, SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(0, horizonY, w, h, groundPaint);

        // ── Clip everything sun-related to above the horizon ──
        canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, w, horizonY));

        // ── Wide soft glow around sun ──
        float glowRadius = sunRadius * (3.0f + (float)eased * 2.0f);
        byte glowAlpha = (byte)(eased * 80);
        using var glowPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx, sunCenterY), glowRadius,
                new[] { new SKColor(0xFF, 0xD0, 0x60, glowAlpha), SKColors.Transparent },
                null, SKShaderTileMode.Clamp)
        };
        canvas.DrawCircle(cx, sunCenterY, glowRadius, glowPaint);

        // ── Solar corona rim + streamers — appear as sun clears horizon ──
        double coronaFraction = Math.Clamp((eased - 0.50) / 0.50, 0, 1);
        if (coronaFraction > 0)
        {
            // Annular corona glow: transparent at centre, bright ring at sun edge, fading out
            float coronaMaxR = sunRadius * 3.2f;
            float sunEdgeFrac = sunRadius / coronaMaxR;
            byte ca = (byte)(coronaFraction * 160);
            using var coronaPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Shader = SKShader.CreateRadialGradient(
                    new SKPoint(cx, sunCenterY), coronaMaxR,
                    new SKColor[]
                    {
                        SKColors.Transparent,
                        SKColors.Transparent,
                        new(0xFF, 0xF4, 0x90, ca),
                        new(0xFF, 0xD0, 0x50, (byte)(ca * 0.35f)),
                        new(0xFF, 0xA0, 0x20, (byte)(ca * 0.08f)),
                        SKColors.Transparent,
                    },
                    new float[]
                    {
                        0f,
                        sunEdgeFrac * 0.90f,
                        sunEdgeFrac * 1.00f,
                        sunEdgeFrac * 1.25f,
                        sunEdgeFrac * 1.75f,
                        1f,
                    },
                    SKShaderTileMode.Clamp)
            };
            canvas.DrawCircle(cx, sunCenterY, coronaMaxR, coronaPaint);

            // Soft corona streamers — thin irregular lines radiating from disc edge
            int streamCount = 14;
            using var streamPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 0.9f,
                StrokeCap = SKStrokeCap.Round,
                Color = new SKColor(0xFF, 0xEC, 0x88, (byte)(coronaFraction * 28))
            };
            for (int i = 0; i < streamCount; i++)
            {
                double angle = 2.0 * Math.PI * i / streamCount;
                // Vary length irregularly to feel organic
                float len = sunRadius * (1.2f + 1.1f * (float)Math.Abs(Math.Sin(angle * 1.7 + 0.4)));
                float ix = cx + sunRadius * (float)Math.Cos(angle);
                float iy = sunCenterY + sunRadius * (float)Math.Sin(angle);
                float ox = cx + (sunRadius + len) * (float)Math.Cos(angle);
                float oy = sunCenterY + (sunRadius + len) * (float)Math.Sin(angle);
                canvas.DrawLine(ix, iy, ox, oy, streamPaint);
            }
        }

        // ── Sun disc ──
        byte sunAlpha = (byte)(60 + eased * 195);
        using var sunPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx - sunRadius * 0.25f, sunCenterY - sunRadius * 0.25f), sunRadius,
                new[] { new SKColor(0xFF, 0xF5, 0x90, sunAlpha), new SKColor(0xFF, 0xA0, 0x20, sunAlpha) },
                null, SKShaderTileMode.Clamp)
        };
        canvas.DrawCircle(cx, sunCenterY, sunRadius, sunPaint);

        canvas.Restore();   // end horizon clip

        // ── Horizon line ──
        using var horizonPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Color = new SKColor(0xFF, 0xFF, 0xFF, 0x70)
        };
        canvas.DrawLine(0, horizonY, w, horizonY, horizonPaint);

        // ── Navigate after animation + hold ──
        if (!_navigated && elapsed > RiseDurationSec + HoldSec)
        {
            _navigated = true;
            _animTimer?.Stop();
            DispatcherQueue.TryEnqueue(() =>
            {
                if (Frame != null)
                    Frame.Navigate(typeof(MainPage));
            });
        }
    }
}
