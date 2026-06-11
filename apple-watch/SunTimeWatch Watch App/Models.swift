import Foundation

struct SunData {
    var sunrise: Date
    var sunset: Date
    var solarNoon: Date
    var altitude: Double
    var hourAngle: Double
}

struct MoonData {
    var phaseFraction: Double       // 0=new, 0.25=first quarter, 0.5=full, 0.75=last
    var illuminatedFraction: Double
    var hourOnDial: Double          // 0–24
}
