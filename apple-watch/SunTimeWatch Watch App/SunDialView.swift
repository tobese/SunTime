import SwiftUI

struct SunDialView: View {
    let now: Date
    let lat: Double
    let lon: Double
    let utcOffsetSeconds: Double

    private enum Pal {
        static let dayInner   = Color(red: 0.290, green: 0.565, blue: 0.851)
        static let dayOuter   = Color(red: 0.529, green: 0.808, blue: 0.922)
        static let nightInner = Color(red: 0.102, green: 0.137, blue: 0.302)
        static let nightOuter = Color(red: 0.051, green: 0.067, blue: 0.169)
        static let sunDisc    = Color(red: 1.000, green: 0.843, blue: 0.000)
        static let sunHi      = Color(red: 1.000, green: 0.945, blue: 0.463)
        static let sunGlow    = Color(red: 1.000, green: 0.647, blue: 0.000, opacity: 0.35)
        static let moonLit    = Color(red: 0.910, green: 0.878, blue: 0.784)
        static let moonDark   = Color(red: 0.102, green: 0.102, blue: 0.180)
        static let moonRim    = Color(red: 0.753, green: 0.722, blue: 0.565, opacity: 0.50)
        static let sunrise    = Color(red: 1.000, green: 0.647, blue: 0.000)
        static let sunset     = Color(red: 1.000, green: 0.388, blue: 0.278)
        static let noon       = Color(red: 1.000, green: 0.843, blue: 0.000)
        static let handMin    = Color.white.opacity(0.73)
        static let bg         = Color(red: 0.071, green: 0.071, blue: 0.125)
    }

    var body: some View {
        Canvas { ctx, size in
            let cx = size.width / 2
            let cy = size.height / 2
            let R  = min(size.width, size.height) / 2.4

            let sun      = SolarCalculator.calculate(latitude: lat, longitude: lon,
                                                     utcNow: now, utcOffsetSeconds: utcOffsetSeconds)
            let localNow = now.addingTimeInterval(utcOffsetSeconds)
            let localH   = Self.totalHours(localNow)
            let moon     = MoonCalculator.calculate(utcNow: now, localHour: localH)

            drawBackground(ctx, size: size, altitude: sun.altitude)
            drawBezel(ctx, cx: cx, cy: cy, R: R)

            // NoonAtTop: rotate so solar noon sits at top (270° in screen convention).
            let noonH      = Self.totalHours(sun.solarNoon.addingTimeInterval(utcOffsetSeconds))
            let noonDeg    = Self.hourToScreenDeg(noonH)
            let rotAngle   = CGFloat((270.0 - noonDeg) * .pi / 180.0)

            var dial = ctx
            dial.concatenate(CGAffineTransform(translationX: cx, y: cy)
                .rotated(by: rotAngle).translatedBy(x: -cx, y: -cy))

            drawSkyGround(dial, cx: cx, cy: cy, R: R, sun: sun)
            drawHorizonLine(dial, cx: cx, cy: cy, R: R)
            drawClockFace(dial, cx: cx, cy: cy, R: R, now: localNow, counterRot: -rotAngle)
            drawArcDots(dial, cx: cx, cy: cy, R: R, sun: sun)
            drawSun(dial, cx: cx, cy: cy, R: R, sun: sun, localNow: localNow)
            drawMoon(dial, cx: cx, cy: cy, R: R, moon: moon, localNow: localNow)

            drawRiseSetBoxes(ctx, cx: cx, cy: cy, R: R, sun: sun)
        }
        .ignoresSafeArea()
        .background(Pal.bg)
    }

    // MARK: - Static geometry helpers

    static func hourToScreenDeg(_ h: Double) -> Double {
        (h / 24.0 * 360.0 - 270.0 + 360.0).truncatingRemainder(dividingBy: 360.0)
    }
    static func hourToAngleRad(_ h: Double) -> Double { hourToScreenDeg(h) * .pi / 180.0 }

    static func arcPt(hour: Double, r: CGFloat, cx: CGFloat, cy: CGFloat) -> CGPoint {
        let a = hourToAngleRad(hour)
        return CGPoint(x: cx + r * CGFloat(cos(a)), y: cy + r * CGFloat(sin(a)))
    }

    static func totalHours(_ date: Date) -> Double {
        var cal = Calendar(identifier: .gregorian)
        cal.timeZone = TimeZone(identifier: "UTC")!
        let c = cal.dateComponents([.hour, .minute, .second], from: date)
        return Double(c.hour ?? 0) + Double(c.minute ?? 0) / 60.0 + Double(c.second ?? 0) / 3600.0
    }

    // MARK: - Background

    private func drawBackground(_ ctx: GraphicsContext, size: CGSize, altitude: Double) {
        var c = ctx
        let (zenith, horizon, ground) = skyPalette(altitude)
        let cy = size.height / 2
        let bg = Path(CGRect(origin: .zero, size: size))
        c.fill(bg, with: .linearGradient(
            Gradient(stops: [
                .init(color: zenith,  location: 0),
                .init(color: horizon, location: cy / size.height),
                .init(color: ground,  location: 1),
            ]),
            startPoint: .zero, endPoint: CGPoint(x: 0, y: size.height)
        ))
    }

    private func skyPalette(_ alt: Double) -> (Color, Color, Color) {
        typealias RGB = (Double, Double, Double)
        typealias Key = (alt: Double, z: RGB, h: RGB, g: RGB)
        func c(_ rgb: RGB) -> Color { Color(red: rgb.0, green: rgb.1, blue: rgb.2) }
        func mix(_ a: RGB, _ b: RGB, _ t: Double) -> RGB {
            (a.0+(b.0-a.0)*t, a.1+(b.1-a.1)*t, a.2+(b.2-a.2)*t)
        }
        let keys: [Key] = [
            (-18, (0.024,0.024,0.063), (0.031,0.031,0.094), (0.020,0.020,0.063)),
            ( -6, (0.051,0.063,0.208), (0.094,0.082,0.290), (0.039,0.043,0.125)),
            (  0, (0.102,0.125,0.314), (0.910,0.376,0.165), (0.078,0.071,0.118)),
            ( 45, (0.290,0.565,0.851), (0.529,0.808,0.922), (0.118,0.157,0.251)),
        ]
        if alt <= keys[0].alt  { return (c(keys[0].z), c(keys[0].h), c(keys[0].g)) }
        if alt >= keys.last!.alt { return (c(keys.last!.z), c(keys.last!.h), c(keys.last!.g)) }
        for i in 0..<keys.count-1 {
            if alt <= keys[i+1].alt {
                let t = (alt - keys[i].alt) / (keys[i+1].alt - keys[i].alt)
                return (c(mix(keys[i].z, keys[i+1].z, t)),
                        c(mix(keys[i].h, keys[i+1].h, t)),
                        c(mix(keys[i].g, keys[i+1].g, t)))
            }
        }
        return (c(keys.last!.z), c(keys.last!.h), c(keys.last!.g))
    }

    // MARK: - Bezel (unrotated)

    private func drawBezel(_ ctx: GraphicsContext, cx: CGFloat, cy: CGFloat, R: CGFloat) {
        var c = ctx
        let outerR = R * 1.18
        let bezelW = outerR - R
        let center = CGPoint(x: cx, y: cy)

        // Steel ring (simple gradient approximation via stroke)
        var ring = Path(); ring.addArc(center: center, radius: (outerR + R) / 2,
                                       startAngle: .zero, endAngle: .radians(2 * .pi), clockwise: false)
        c.stroke(ring, with: .linearGradient(
            Gradient(colors: [Color(white: 0.31), Color(white: 0.72), Color(white: 0.82), Color(white: 0.66), Color(white: 0.31)]),
            startPoint: CGPoint(x: cx - outerR, y: cy), endPoint: CGPoint(x: cx + outerR, y: cy)
        ), lineWidth: bezelW)

        // Inner shadow edge
        var inner = Path(); inner.addArc(center: center, radius: R,
                                          startAngle: .zero, endAngle: .radians(2 * .pi), clockwise: false)
        c.stroke(inner, with: .color(Color(white: 0.09, opacity: 0.82)), lineWidth: 1.5)

        // 60 tick marks
        for i in 0..<60 {
            let angle = CGFloat(Double.pi / 2.0 - Double(i) * (2.0 * Double.pi / 60.0))
            let co = CGFloat(cos(angle)); let si = CGFloat(sin(angle))
            let major = i % 5 == 0
            let tickLen: CGFloat = major ? bezelW * 0.36 : bezelW * 0.20
            let inner2 = outerR - tickLen
            var tick = Path()
            tick.move(to: CGPoint(x: cx + inner2 * co, y: cy - inner2 * si))
            tick.addLine(to: CGPoint(x: cx + (outerR - 1) * co, y: cy - (outerR - 1) * si))
            c.stroke(tick, with: .color(major ? Color(white: 0.89) : Color(white: 0.66, opacity: 0.80)),
                     lineWidth: major ? 1.5 : 0.8)
        }

        // Triangle pointer at 12 o'clock (top, fixed)
        let triTip  = outerR - bezelW * 0.07
        let triBase = outerR - bezelW * 0.52
        let triHW   = bezelW * 0.20
        var tri = Path()
        tri.move(to: CGPoint(x: cx, y: cy - triTip))
        tri.addLine(to: CGPoint(x: cx - triHW, y: cy - triBase))
        tri.addLine(to: CGPoint(x: cx + triHW, y: cy - triBase))
        tri.closeSubpath()
        c.fill(tri, with: .color(Color(white: 0.94)))
    }

    // MARK: - Sky / Ground arcs

    private func drawSkyGround(_ ctx: GraphicsContext, cx: CGFloat, cy: CGFloat, R: CGFloat, sun: SunData) {
        var c = ctx
        let center = CGPoint(x: cx, y: cy)
        let riseH = Self.totalHours(sun.sunrise.addingTimeInterval(utcOffsetSeconds))
        let setH  = Self.totalHours(sun.sunset.addingTimeInterval(utcOffsetSeconds))
        let riseA = Angle.radians(Self.hourToAngleRad(riseH))
        let setA  = Angle.radians(Self.hourToAngleRad(setH))

        // Day wedge (sunrise → sunset clockwise through noon)
        var day = Path(); day.move(to: center)
        day.addArc(center: center, radius: R, startAngle: riseA, endAngle: setA, clockwise: false)
        day.closeSubpath()
        var dc = c
        dc.fill(day, with: .radialGradient(
            Gradient(colors: [Pal.dayInner, Pal.dayOuter]),
            center: center, startRadius: 0, endRadius: R))

        // Night wedge (sunset → sunrise clockwise through midnight)
        var night = Path(); night.move(to: center)
        night.addArc(center: center, radius: R, startAngle: setA, endAngle: riseA, clockwise: false)
        night.closeSubpath()
        var nc = c
        nc.fill(night, with: .radialGradient(
            Gradient(colors: [Pal.nightInner, Pal.nightOuter]),
            center: center, startRadius: 0, endRadius: R))

        // Ring outline
        var ring = Path()
        ring.addArc(center: center, radius: R, startAngle: .zero, endAngle: .radians(2 * .pi), clockwise: false)
        c.stroke(ring, with: .color(.white.opacity(0.25)), lineWidth: 2)
    }

    // MARK: - Horizon line

    private func drawHorizonLine(_ ctx: GraphicsContext, cx: CGFloat, cy: CGFloat, R: CGFloat) {
        var c = ctx
        var line = Path()
        line.move(to: CGPoint(x: cx - R, y: cy))
        line.addLine(to: CGPoint(x: cx + R, y: cy))
        c.stroke(line, with: .color(.white.opacity(0.50)),
                 style: StrokeStyle(lineWidth: 1.5, dash: [6, 4]))
    }

    // MARK: - Clock face

    private func drawClockFace(_ ctx: GraphicsContext, cx: CGFloat, cy: CGFloat, R: CGFloat,
                                now: Date, counterRot: CGFloat) {
        var c = ctx
        let center = CGPoint(x: cx, y: cy)
        let rInner = R * 0.58

        // 24 ticks
        for h in 0..<24 {
            let a = Self.hourToAngleRad(Double(h))
            let co = CGFloat(cos(a)); let si = CGFloat(sin(a))
            let inner: CGFloat = h % 6 == 0 ? 0.82 : 0.90
            var tick = Path()
            tick.move(to: CGPoint(x: cx + rInner * inner * co, y: cy + rInner * inner * si))
            tick.addLine(to: CGPoint(x: cx + rInner * co, y: cy + rInner * si))
            c.stroke(tick, with: .color(.white.opacity(0.6)), lineWidth: 1.5)

            if h % 6 == 0 {
                let nr  = rInner * 1.14
                let tx  = cx + nr * co
                let ty  = cy + nr * si
                var lc  = c
                lc.concatenate(CGAffineTransform(translationX: tx, y: ty)
                    .rotated(by: counterRot).translatedBy(x: -tx, y: -ty))
                lc.draw(Text(verbatim: "\(h)").font(.system(size: rInner * 0.18)).foregroundColor(.white.opacity(0.7)),
                        at: CGPoint(x: tx, y: ty), anchor: .center)
            }
        }

        // Minute hand (standard 60-min sweep, 0=top)
        var cal = Calendar(identifier: .gregorian); cal.timeZone = TimeZone(identifier: "UTC")!
        let comps = cal.dateComponents([.hour, .minute], from: now)
        let minFrac  = Double(comps.minute ?? 0) / 60.0
        let minDeg   = (270.0 - minFrac * 360.0 + 360.0).truncatingRemainder(dividingBy: 360.0)
        let minRad   = minDeg * .pi / 180.0
        let mLen     = rInner * 0.72
        var minPath  = Path()
        minPath.move(to: center)
        minPath.addLine(to: CGPoint(x: cx + mLen * CGFloat(cos(minRad)),
                                   y: cy + mLen * CGFloat(sin(minRad))))
        c.stroke(minPath, with: .color(Pal.handMin), style: StrokeStyle(lineWidth: 1.5, lineCap: .round))

        // Hour hand (24h arc position)
        let hourH    = Double(comps.hour ?? 0) + Double(comps.minute ?? 0) / 60.0
        let hourRad  = Self.hourToAngleRad(hourH)
        let hLen     = rInner * 0.55
        var hourPath = Path()
        hourPath.move(to: center)
        hourPath.addLine(to: CGPoint(x: cx + hLen * CGFloat(cos(hourRad)),
                                    y: cy + hLen * CGFloat(sin(hourRad))))
        c.stroke(hourPath, with: .color(Pal.noon), style: StrokeStyle(lineWidth: 3, lineCap: .round))

        // Centre dot
        var dot = Path(); dot.addEllipse(in: CGRect(x: cx - 2, y: cy - 2, width: 4, height: 4))
        c.fill(dot, with: .color(.white))
    }

    // MARK: - Arc dots

    private func drawArcDots(_ ctx: GraphicsContext, cx: CGFloat, cy: CGFloat, R: CGFloat, sun: SunData) {
        var c = ctx
        func dot(_ date: Date, _ color: Color) {
            let h = Self.totalHours(date.addingTimeInterval(utcOffsetSeconds))
            let pt = Self.arcPt(hour: h, r: R, cx: cx, cy: cy)
            var d = Path(); d.addEllipse(in: CGRect(x: pt.x - 4, y: pt.y - 4, width: 8, height: 8))
            c.fill(d, with: .color(color))
        }
        dot(sun.sunrise, Pal.sunrise)
        dot(sun.sunset,  Pal.sunset)
        dot(sun.solarNoon, Pal.noon)
    }

    // MARK: - Sun

    private func drawSun(_ ctx: GraphicsContext, cx: CGFloat, cy: CGFloat, R: CGFloat,
                         sun: SunData, localNow: Date) {
        var c = ctx
        let nowH = Self.totalHours(localNow)
        let pt   = Self.arcPt(hour: nowH, r: R, cx: cx, cy: cy)
        let riseH = Self.totalHours(sun.sunrise.addingTimeInterval(utcOffsetSeconds))
        let setH  = Self.totalHours(sun.sunset.addingTimeInterval(utcOffsetSeconds))
        let above = nowH >= riseH && nowH < setH
        let sr: CGFloat = above ? R * 0.070 : R * 0.045
        let alpha: Double = above ? 1.0 : 0.27

        // Glow
        var glow = Path(); glow.addEllipse(in: CGRect(x: pt.x - sr*3, y: pt.y - sr*3, width: sr*6, height: sr*6))
        var gc = c
        gc.fill(glow, with: .radialGradient(
            Gradient(colors: [Pal.sunGlow.opacity(alpha/3), .clear]),
            center: pt, startRadius: 0, endRadius: sr * 3))

        // Disc
        var disc = Path(); disc.addEllipse(in: CGRect(x: pt.x - sr, y: pt.y - sr, width: sr*2, height: sr*2))
        var dc = c
        dc.fill(disc, with: .radialGradient(
            Gradient(colors: [Pal.sunHi.opacity(alpha), Pal.sunrise.opacity(alpha)]),
            center: CGPoint(x: pt.x - sr*0.3, y: pt.y - sr*0.3),
            startRadius: 0, endRadius: sr))
    }

    // MARK: - Moon

    private func drawMoon(_ ctx: GraphicsContext, cx: CGFloat, cy: CGFloat, R: CGFloat,
                          moon: MoonData, localNow: Date) {
        let nowH  = Self.totalHours(localNow)
        var diff  = abs(moon.hourOnDial - nowH)
        if diff > 12 { diff = 24 - diff }
        if diff < 0.25 { return }

        let pt = Self.arcPt(hour: moon.hourOnDial, r: R, cx: cx, cy: cy)
        let mr: CGFloat = R * 0.055
        let frac   = moon.phaseFraction
        let termX  = mr * CGFloat(cos(frac * 2 * .pi))
        let waxing = frac < 0.5

        let discRect = CGRect(x: pt.x - mr, y: pt.y - mr, width: mr*2, height: mr*2)
        var discPath = Path(); discPath.addEllipse(in: discRect)

        // Clipped layers
        var clipped = ctx
        clipped.clip(to: discPath)

        var dark = clipped; dark.fill(discPath, with: .color(Pal.moonDark))

        var halfPath = Path()
        if waxing {
            halfPath.move(to: pt)
            halfPath.addLine(to: CGPoint(x: pt.x, y: pt.y - mr))
            halfPath.addArc(center: pt, radius: mr, startAngle: .degrees(-90), endAngle: .degrees(90), clockwise: false)
        } else {
            halfPath.move(to: pt)
            halfPath.addLine(to: CGPoint(x: pt.x, y: pt.y + mr))
            halfPath.addArc(center: pt, radius: mr, startAngle: .degrees(90), endAngle: .degrees(270), clockwise: false)
        }
        halfPath.closeSubpath()
        var lit = clipped; lit.fill(halfPath, with: .color(Pal.moonLit))

        if abs(termX) > 0.5 {
            let ew = abs(termX)
            let termColor: Color = waxing ? (termX > 0 ? Pal.moonDark : Pal.moonLit)
                                          : (termX < 0 ? Pal.moonLit  : Pal.moonDark)
            var ellipsePath = Path()
            ellipsePath.addEllipse(in: CGRect(x: pt.x - ew, y: pt.y - mr, width: ew*2, height: mr*2))
            var term = clipped; term.fill(ellipsePath, with: .color(termColor))
        }

        // Rim (unclipped)
        var rim = ctx; rim.stroke(discPath, with: .color(Pal.moonRim), lineWidth: 0.8)
    }

    // MARK: - Rise / Set boxes (fixed, outside rotation)

    private func drawRiseSetBoxes(_ ctx: GraphicsContext, cx: CGFloat, cy: CGFloat, R: CGFloat, sun: SunData) {
        drawTimeBox(ctx, cx: cx, cy: cy, R: R, time: sun.sunrise, isRise: true)
        drawTimeBox(ctx, cx: cx, cy: cy, R: R, time: sun.sunset,  isRise: false)
    }

    private func drawTimeBox(_ ctx: GraphicsContext, cx: CGFloat, cy: CGFloat, R: CGFloat,
                              time: Date, isRise: Bool) {
        var c = ctx
        var cal = Calendar(identifier: .gregorian); cal.timeZone = TimeZone(identifier: "UTC")!
        let t    = time.addingTimeInterval(utcOffsetSeconds)
        let comps = cal.dateComponents([.hour, .minute], from: t)
        let label = String(format: "%02d:%02d", comps.hour ?? 0, comps.minute ?? 0)

        let boxW: CGFloat = R * 0.42
        let boxH: CGFloat = R * 0.22
        let rowY  = cy - R * 0.65
        let fieldX = isRise ? cx - R * 0.54 : cx + R * 0.54
        let rect  = CGRect(x: fieldX - boxW/2, y: rowY - boxH/2, width: boxW, height: boxH)

        var bg = Path(roundedRect: rect, cornerRadius: R * 0.028)
        c.fill(bg, with: .color(Color(white: 0.09, opacity: 0.80)))
        var border = Path(roundedRect: rect, cornerRadius: R * 0.028)
        c.stroke(border, with: .color(Color(white: 0.69, opacity: 0.80)), lineWidth: 1.1)

        let color = isRise ? Pal.sunrise : Pal.sunset
        c.draw(Text(verbatim: label).font(.system(size: R * 0.10, weight: .medium, design: .monospaced))
               .foregroundColor(color),
               at: CGPoint(x: fieldX, y: rowY), anchor: .center)
    }
}
