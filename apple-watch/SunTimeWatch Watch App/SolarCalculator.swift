import Foundation

enum SolarCalculator {
    static func calculate(latitude: Double, longitude: Double,
                          utcNow: Date, utcOffsetSeconds: Double) -> SunData {
        let jd = julianDay(utcNow)
        let T = (jd - 2451545.0) / 36525.0

        let L0 = fmod(280.46646 + T * (36000.76983 + 0.0003032 * T), 360.0)
        let M  = 357.52911 + T * (35999.05029 - 0.0001537 * T)
        let e  = 0.016708634 - T * (0.000042037 + 0.0000001267 * T)

        let Mrad = rad(M)
        let C = sin(Mrad) * (1.914602 - T * (0.004817 + 0.000014 * T))
              + sin(2 * Mrad) * (0.019993 - 0.000101 * T)
              + sin(3 * Mrad) * 0.000289

        let sunTrueLong = L0 + C
        let omega = 125.04 - 1934.136 * T
        let sunAppLong = sunTrueLong - 0.00569 - 0.00478 * sin(rad(omega))

        let obliq0 = 23.0 + (26.0 + (21.448 - T * (46.815 + T * (0.00059 - T * 0.001813))) / 60.0) / 60.0
        let obliq  = obliq0 + 0.00256 * cos(rad(omega))

        let declination = deg(asin(sin(rad(obliq)) * sin(rad(sunAppLong))))

        let y = pow(tan(rad(obliq / 2)), 2)
        let eqTime = 4.0 * deg(
            y * sin(2 * rad(L0))
            - 2.0 * e * sin(Mrad)
            + 4.0 * e * y * sin(Mrad) * cos(2 * rad(L0))
            - 0.5 * y * y * sin(4 * rad(L0))
            - 1.25 * e * e * sin(2 * Mrad))

        let offsetMin = utcOffsetSeconds / 60.0
        let solarNoonMin = 720.0 - 4.0 * longitude - eqTime + offsetMin

        let latRad = rad(latitude)
        let decRad = rad(declination)
        var cosHA = cos(rad(90.833)) / (cos(latRad) * cos(decRad)) - tan(latRad) * tan(decRad)
        cosHA = max(-1.0, min(1.0, cosHA))
        let ha = deg(acos(cosHA))

        let sunriseMin = solarNoonMin - ha * 4.0
        let sunsetMin  = solarNoonMin + ha * 4.0

        let cal = Calendar(identifier: .gregorian)
        let localNow = utcNow.addingTimeInterval(utcOffsetSeconds)
        var comps = cal.dateComponents([.year, .month, .day], from: localNow)
        comps.hour = 0; comps.minute = 0; comps.second = 0
        let midnight = cal.date(from: comps)!.addingTimeInterval(-utcOffsetSeconds)

        let sunrise   = midnight.addingTimeInterval(sunriseMin * 60)
        let sunset    = midnight.addingTimeInterval(sunsetMin  * 60)
        let solarNoon = midnight.addingTimeInterval(solarNoonMin * 60)

        let utcCal = Calendar(identifier: .gregorian)
        let utcComps = utcCal.dateComponents(in: TimeZone(identifier: "UTC")!, from: utcNow)
        let utcMinutes = Double((utcComps.hour ?? 0) * 60 + (utcComps.minute ?? 0))
                       + Double(utcComps.second ?? 0) / 60.0

        let tst = fmod(utcMinutes + eqTime + 4.0 * longitude + 1440.0, 1440.0)
        let hourAngle = tst / 4.0 - 180.0

        let sinAlt = sin(latRad) * sin(decRad)
                   + cos(latRad) * cos(decRad) * cos(rad(hourAngle))
        let altitude = deg(asin(max(-1.0, min(1.0, sinAlt))))

        return SunData(sunrise: sunrise, sunset: sunset, solarNoon: solarNoon,
                       altitude: altitude, hourAngle: hourAngle)
    }

    private static func rad(_ d: Double) -> Double { d * .pi / 180.0 }
    private static func deg(_ r: Double) -> Double { r * 180.0 / .pi }

    private static func julianDay(_ date: Date) -> Double {
        var cal = Calendar(identifier: .gregorian)
        cal.timeZone = TimeZone(identifier: "UTC")!
        let comps = cal.dateComponents([.year, .month, .day, .hour, .minute, .second], from: date)
        var y = comps.year!
        var m = comps.month!
        let d = Double(comps.day!) + (Double(comps.hour!) * 3600
              + Double(comps.minute!) * 60 + Double(comps.second!)) / 86400.0
        if m <= 2 { y -= 1; m += 12 }
        let A = y / 100
        let B = 2 - A + A / 4
        return Double(Int(365.25 * Double(y + 4716)))
             + Double(Int(30.6001 * Double(m + 1)))
             + d + Double(B) - 1524.5
    }
}
