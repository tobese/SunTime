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

    private const double RiseDurationSec = 4.5;
    private const double HoldSec = 1.2;
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

        // Sun travels from 3 radii below horizon to 2.5 radii above
        float sunCenterY = horizonY + sunRadius * 3.0f
                           - (float)(eased * sunRadius * 5.5f);

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

        // ── Soft corona rays — tapered wedges, low opacity ──
        // Appear gradually as sun rises above horizon
        double rayFraction = Math.Clamp((eased - 0.45) / 0.55, 0, 1);
        if (rayFraction > 0)
        {
            int rayCount = 16;
            float rayInner = sunRadius * 1.08f;
            float rayOuter = sunRadius * (1.9f + (float)rayFraction * 1.8f);
            double halfWedge = Math.PI / rayCount * 0.55;  // half-angle of each wedge
            byte rayAlpha = (byte)(rayFraction * 38);       // very subtle

            using var rayPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = new SKColor(0xFF, 0xE0, 0x80, rayAlpha)
            };
            using var rayPath = new SKPath();

            for (int i = 0; i < rayCount; i++)
            {
                double angle = 2.0 * Math.PI * i / rayCount;

                float ax = cx + rayInner * (float)Math.Cos(angle - halfWedge);
                float ay = sunCenterY + rayInner * (float)Math.Sin(angle - halfWedge);
                float bx = cx + rayInner * (float)Math.Cos(angle + halfWedge);
                float by = sunCenterY + rayInner * (float)Math.Sin(angle + halfWedge);
                float tx = cx + rayOuter * (float)Math.Cos(angle);
                float ty = sunCenterY + rayOuter * (float)Math.Sin(angle);

                rayPath.MoveTo(ax, ay);
                rayPath.LineTo(tx, ty);
                rayPath.LineTo(bx, by);
                rayPath.Close();
                canvas.DrawPath(rayPath, rayPaint);
                rayPath.Reset();
            }

            // Second pass — offset by half a ray spacing, even more subtle
            byte rayAlpha2 = (byte)(rayFraction * 22);
            using var rayPaint2 = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = new SKColor(0xFF, 0xD0, 0x60, rayAlpha2)
            };
            for (int i = 0; i < rayCount; i++)
            {
                double angle = 2.0 * Math.PI * i / rayCount + Math.PI / rayCount;
                float ax = cx + rayInner * (float)Math.Cos(angle - halfWedge);
                float ay = sunCenterY + rayInner * (float)Math.Sin(angle - halfWedge);
                float bx = cx + rayInner * (float)Math.Cos(angle + halfWedge);
                float by = sunCenterY + rayInner * (float)Math.Sin(angle + halfWedge);
                float tx = cx + rayOuter * (float)Math.Cos(angle);
                float ty = sunCenterY + rayOuter * (float)Math.Sin(angle);

                rayPath.MoveTo(ax, ay);
                rayPath.LineTo(tx, ty);
                rayPath.LineTo(bx, by);
                rayPath.Close();
                canvas.DrawPath(rayPath, rayPaint2);
                rayPath.Reset();
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
