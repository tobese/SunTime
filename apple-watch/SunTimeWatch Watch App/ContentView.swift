import SwiftUI

struct ContentView: View {
    @State private var location = LocationManager()

    var body: some View {
        TimelineView(.periodic(from: .now, by: 60)) { ctx in
            SunDialView(
                now: ctx.date,
                lat: location.latitude,
                lon: location.longitude,
                utcOffsetSeconds: location.utcOffsetSeconds
            )
        }
        .ignoresSafeArea()
    }
}
