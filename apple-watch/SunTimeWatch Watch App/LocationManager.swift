import CoreLocation
import Observation

@Observable
final class LocationManager: NSObject, CLLocationManagerDelegate {
    var latitude:  Double = 59.33   // Stockholm default
    var longitude: Double = 18.07
    var utcOffsetSeconds: Double = Double(TimeZone.current.secondsFromGMT(for: .now))

    private let manager = CLLocationManager()

    override init() {
        super.init()
        manager.delegate = self
        manager.desiredAccuracy = kCLLocationAccuracyThreeKilometers
        manager.requestWhenInUseAuthorization()
        manager.startUpdatingLocation()
    }

    func locationManager(_ manager: CLLocationManager, didUpdateLocations locations: [CLLocation]) {
        guard let loc = locations.last else { return }
        latitude  = loc.coordinate.latitude
        longitude = loc.coordinate.longitude
        utcOffsetSeconds = Double(TimeZone.current.secondsFromGMT(for: loc.timestamp))
        self.manager.stopUpdatingLocation()
    }
}
