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
        canvas.Clear(new SKColor(0x12, 0x12, 0x20));

        float w = info.Width;
        float h = info.Height;
        float cx = w / 2f;
        float horizonY = h * 0.55f;
        float sunRadius = Math.Min(w, h) * 0.12f;

        double elapsed = _clock.Elapsed.TotalSeconds;
        double t = Math.Clamp(elapsed / RiseDurationSec, 0, 1);

        // Ease-out cubic — slow start, decelerates into final position
        double eased = 1.0 - Math.Pow(1.0 - t, 3);

        // Sun travels from 5 radii below horizon to 2.5 radii above
        float sunCenterY = horizonY + sunRadius * 5.0f
                           - (float)(eased * sunRadius * 7.5f);

        // ── Pre-dawn horizon glow (starts immediately, peaks near sunrise) ──
        double glowPeak = Math.Clamp(eased * 2.0, 0, 1);   // peaks halfway through rise
        byte horizonGlowAlpha = (byte)(glowPeak * 160);
        using var preDawnPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(cx, horizonY - sunRadius * 4),
                new SKPoint(cx, horizonY),
                new SKColor[]
                {
                    new(0x12, 0x12, 0x20, 0),
                    new(0xFF, 0x80, 0x20, (byte)(horizonGlowAlpha * 0.4f)),
                    new(0xFF, 0xA0, 0x30, horizonGlowAlpha),
                },
                new float[] { 0f, 0.6f, 1f },
                SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(0, 0, w, horizonY, preDawnPaint);

        // ── Sky brightens as sun clears horizon ──
        double skyFraction = Math.Clamp((eased - 0.4) / 0.6, 0, 1);
        if (skyFraction > 0)
        {
            byte skyAlpha = (byte)(skyFraction * 130);
            using var skyPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Shader = SKShader.CreateLinearGradient(
                    new SKPoint(cx, 0),
                    new SKPoint(cx, horizonY),
                    new SKColor[]
                    {
                        new(0x1A, 0x38, 0x6E, skyAlpha),
                        new(0xFF, 0x90, 0x40, skyAlpha),
                    },
                    null, SKShaderTileMode.Clamp)
            };
            canvas.DrawRect(0, 0, w, horizonY, skyPaint);
        }

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
