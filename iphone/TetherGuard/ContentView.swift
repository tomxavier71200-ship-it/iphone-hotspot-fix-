import SwiftUI

struct ContentView: View {
    @ObservedObject var diagnostics: PhoneDiagnostics

    var body: some View {
        NavigationStack {
            List {
                Section {
                    VStack(alignment: .leading, spacing: 12) {
                        Label("YOUR CONNECTION COMPANION", systemImage: "personalhotspot")
                            .font(.caption.weight(.semibold)).foregroundStyle(.tint)
                        Text("Connect by cable.")
                            .font(.largeTitle.bold())
                        Text("Share your cellular internet over USB. The Windows helper verifies the route, shows live USB traffic and watches for interruptions.")
                            .foregroundStyle(.secondary)
                    }
                    .padding(.vertical, 12)
                }

                Section {
                    LabeledContent("Network path", value: diagnostics.network)
                    LabeledContent("Battery", value: diagnostics.battery)
                    VStack(alignment: .leading, spacing: 8) {
                        Text(diagnostics.result)
                            .accessibilityAddTraits(.updatesFrequently)
                        if let date = diagnostics.checkedAt {
                            Text("Last completed test: \(date.formatted(date: .omitted, time: .standard))")
                                .font(.caption).foregroundStyle(.secondary)
                        }
                    }.padding(.vertical, 4)
                    Button(action: diagnostics.testInternet) {
                        HStack {
                            Text(diagnostics.testing ? "Checking…" : "Test iPhone internet")
                            Spacer()
                            if diagnostics.testing { ProgressView() }
                            else { Image(systemName: "arrow.up.arrow.down") }
                        }
                    }.disabled(diagnostics.testing)
                } header: {
                    Text("This iPhone")
                } footer: {
                    Text("These are phone diagnostics. They do not confirm Personal Hotspot status or the Windows connection. Diagnostics refresh while this app is open.")
                }

                Section("Set up tethering") {
                    step(1, "Prepare Windows", "Install Apple Devices, then connect a USB cable that supports data.")
                    step(2, "Enable Personal Hotspot", "In Settings → Personal Hotspot, turn on Allow Others to Join. Unlock iPhone and tap Trust if prompted.")
                    step(3, "Give cellular a good connection", "Keep iPhone cool, place it where cellular reception is good, and check that your carrier plan supports tethering. Connect directly to the PC when possible.")
                    step(4, "Start the Windows helper", "Select the Apple Mobile Device Ethernet adapter and start monitoring. Keep PC awake can help prevent automatic Windows sleep during your session.")
                }

                Section("If the connection drops") {
                    Label("Check cellular service and your data allowance.", systemImage: "antenna.radiowaves.left.and.right")
                    Label("Reconnect USB, unlock iPhone and check for a Trust prompt.", systemImage: "cable.connector")
                    Label("Check Allow Others to Join if Wi-Fi fallback cannot connect.", systemImage: "personalhotspot")
                    Label("Use Keep PC awake in the Windows helper during a USB session.", systemImage: "desktopcomputer")
                }

                Section("What to expect") {
                    Text("iOS controls Personal Hotspot. This app cannot switch it on, override idle behavior, or guarantee uninterrupted tethering. The Windows helper can reconnect only when the hotspot is available.")
                    Text("Your maximum speed depends on cellular reception, network load, your carrier plan and the USB connection. This app cannot boost cellular capacity or bypass limits. Windows controls which internet connection it uses.")
                }

                Section("Privacy") {
                    Text("No account or pairing is needed. A manual internet test contacts Microsoft and, if needed, Apple. There is no analytics or app backend. The Windows helper stores its settings and diagnostic logs locally.")
                }
            }
            .navigationTitle("TetherGuard")
            .navigationBarTitleDisplayMode(.inline)
            .tint(.teal)
        }
    }

    private func step(_ number: Int, _ title: String, _ detail: String) -> some View {
        HStack(alignment: .top, spacing: 12) {
            Text("\(number)")
                .font(.subheadline.bold())
                .foregroundStyle(.teal)
                .frame(width: 28, height: 28)
                .background(.teal.opacity(0.12), in: Circle())
                .accessibilityHidden(true)
            VStack(alignment: .leading, spacing: 4) {
                Text(title).font(.headline)
                Text(detail).font(.subheadline).foregroundStyle(.secondary)
            }
        }.padding(.vertical, 5)
    }
}
