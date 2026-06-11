import Foundation

enum MoonCalculator {
    private static let newMoonJD     = 2451550.26
    private static let synodicPeriod = 29.530588853

    static func calculate(utcNow: Date, localHour: Double) -> MoonData {
        let jd       = julianDay(utcNow)
        let ageDays  = fmod(fmod(jd - newMoonJD, synodicPeriod) + synodicPeriod, synodicPeriod)
        let fraction = ageDays / synodicPeriod
        let illum    = (1.0 - cos(fraction * 2.0 * .pi)) / 2.0
        let hourOnDial = fmod(localHour + fraction * 24.0, 24.0)
        return MoonData(phaseFraction: fraction, illuminatedFraction: illum, hourOnDial: hourOnDial)
    }

    private static func julianDay(_ date: Date) -> Double {
        var cal = Calendar(identifier: .gregorian)
        cal.timeZone = TimeZone(identifier: "UTC")!
        let c = cal.dateComponents([.year, .month, .day, .hour, .minute, .second], from: date)
        var y = c.year!; var m = c.month!
        let d = Double(c.day!) + (Double(c.hour!) * 3600 + Double(c.minute!) * 60 + Double(c.second!)) / 86400.0
        if m <= 2 { y -= 1; m += 12 }
        let A = y / 100; let B = 2 - A + A / 4
        return Double(Int(365.25 * Double(y + 4716))) + Double(Int(30.6001 * Double(m + 1))) + d + Double(B) - 1524.5
    }
}
