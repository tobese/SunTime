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

    // Animation: sun rises from below horizon to above over ~2 seconds,
    // then we hold briefly and navigate.
    private const double RiseDurationSec = 2.0;
    private const double HoldSec = 0.6;
    private bool _navigated;

    public SplashPage()
    {
        this.InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // ~60 fps
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

        // Ease-out cubic
        double eased = 1.0 - Math.Pow(1.0 - t, 3);

        // Sun center: starts 1.5 radii below horizon, rises to 1.5 radii above
        float sunCenterY = horizonY + sunRadius * 1.5f - (float)(eased * sunRadius * 3.0f);

        // Sky brightens as the sun rises
        byte skyAlpha = (byte)(eased * 180);

        // Sky gradient overlay (brightening)
        using var skyPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(cx, 0),
                new SKPoint(cx, horizonY),
                new[] { new SKColor(0x4A, 0x90, 0xD9, skyAlpha), new SKColor(0xFF, 0xA0, 0x50, skyAlpha) },
                null, SKShaderTileMode.Clamp)
        };
        canvas.DrawRect(0, 0, w, horizonY, skyPaint);

        // Sun glow
        float glowRadius = sunRadius * (2.5f + (float)eased * 1.5f);
        byte glowAlpha = (byte)(eased * 100);
        using var glowPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx, sunCenterY), glowRadius,
                new[] { new SKColor(0xFF, 0xD7, 0x00, glowAlpha), SKColors.Transparent },
                null, SKShaderTileMode.Clamp)
        };
        canvas.DrawCircle(cx, sunCenterY, glowRadius, glowPaint);

        // Sun disc
        byte sunAlpha = (byte)(80 + eased * 175);
        using var sunPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(cx - sunRadius * 0.2f, sunCenterY - sunRadius * 0.2f), sunRadius,
                new[] { new SKColor(0xFF, 0xF1, 0x76, sunAlpha), new SKColor(0xFF, 0xA5, 0x00, sunAlpha) },
                null, SKShaderTileMode.Clamp)
        };
        canvas.DrawCircle(cx, sunCenterY, sunRadius, sunPaint);

        // Rays (appear as sun clears horizon)
        if (eased > 0.3)
        {
            float rayAlpha = (float)((eased - 0.3) / 0.7);
            using var rayPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2.5f,
                StrokeCap = SKStrokeCap.Round,
                Color = new SKColor(0xFF, 0xD7, 0x00, (byte)(rayAlpha * 200))
            };

            int rayCount = 8;
            float innerR = sunRadius * 1.3f;
            float outerR = sunRadius * 1.3f + sunRadius * 0.8f * (float)eased;
            for (int i = 0; i < rayCount; i++)
            {
                double angle = Math.PI * i / (rayCount - 1); // semicircle above
                float cos = (float)Math.Cos(angle);
                float sin = (float)Math.Sin(angle);
                canvas.DrawLine(
                    cx - innerR * cos, sunCenterY - innerR * sin,
                    cx - outerR * cos, sunCenterY - outerR * sin,
                    rayPaint);
            }
        }

        // Horizon line
        using var horizonPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            Color = new SKColor(0xFF, 0xFF, 0xFF, 0x80)
        };
        canvas.DrawLine(0, horizonY, w, horizonY, horizonPaint);

        // App title
        float titleAlpha = (float)Math.Clamp(eased * 1.5, 0, 1);
        float titleSize = Math.Min(w, h) * 0.08f;
        using var titleFont = new SKFont(
            SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold,
                SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            titleSize);
        using var titlePaint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor(0xFF, 0xFF, 0xFF, (byte)(titleAlpha * 230))
        };
        canvas.DrawText("SunTime", cx, horizonY + titleSize * 2.0f,
            SKTextAlign.Center, titleFont, titlePaint);

        // Navigate after animation + hold
        if (!_navigated && elapsed > RiseDurationSec + HoldSec)
        {
            _navigated = true;
            _animTimer?.Stop();
            DispatcherQueue.TryEnqueue(() =>
            {
                if (Frame != null)
                {
                    // Hand off to the login flow. LoginPage is responsible
                    // for routing on to MainPage or PendingApprovalPage based
                    // on the authenticated user's status.
                    Frame.Navigate(typeof(Pages.LoginPage));
                    Frame.BackStack.Clear();
                }
            });
        }
    }
}
