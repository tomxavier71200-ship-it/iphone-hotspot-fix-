import SwiftUI

@main
struct TetherGuardApp: App {
    @StateObject private var diagnostics = PhoneDiagnostics()
    @Environment(\.scenePhase) private var scenePhase

    var body: some Scene {
        WindowGroup {
            ContentView(diagnostics: diagnostics)
                .onChange(of: scenePhase, initial: true) { _, phase in
                    if phase == .active { diagnostics.activate() }
                    else { diagnostics.deactivate() }
                }
        }
    }
}
