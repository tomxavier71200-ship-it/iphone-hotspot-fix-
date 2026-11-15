import SwiftUI
import Network
import Combine

@MainActor
final class PhoneDiagnostics: ObservableObject {
    @Published private(set) var network = "Checking…"
    @Published private(set) var battery = "Checking…"
    @Published private(set) var result = "Run a test to verify this iPhone’s internet access."
    @Published private(set) var testing = false
    @Published private(set) var checkedAt: Date?
    private var monitor: NWPathMonitor?
    private var testTask: Task<Void, Never>?
    private var batteryObserver: AnyCancellable?
    private var generation = UUID()

    func activate() {
        guard monitor == nil else { return }
        UIDevice.current.isBatteryMonitoringEnabled = true
        refreshBattery()
        batteryObserver = NotificationCenter.default.publisher(for: UIDevice.batteryLevelDidChangeNotification)
            .merge(with: NotificationCenter.default.publisher(for: UIDevice.batteryStateDidChangeNotification))
            .receive(on: DispatchQueue.main)
            .sink { [weak self] _ in self?.refreshBattery() }
        let next = NWPathMonitor()
        let current = UUID()
        generation = current
        next.pathUpdateHandler = { [weak self] path in
            let description: String
            if path.status == .satisfied {
                description = path.usesInterfaceType(.cellular) ? "Cellular path available" :
                    path.usesInterfaceType(.wifi) ? "Wi-Fi path available" : "Network path available"
            } else if path.status == .requiresConnection {
                description = "Network needs a connection"
            } else { description = "No network path" }
            Task { @MainActor [weak self] in
                guard let self, self.generation == current else { return }
                self.network = description
            }
        }
        monitor = next
        next.start(queue: DispatchQueue(label: "com.tetherguard.path"))
    }

    func deactivate() {
        generation = UUID()
        monitor?.cancel()
        monitor = nil
        batteryObserver?.cancel()
        batteryObserver = nil
        UIDevice.current.isBatteryMonitoringEnabled = false
        if testing { result = "Test paused. Run it again when the app is open." }
        testTask?.cancel()
        testTask = nil
        testing = false
    }

    private func refreshBattery() {
        let level = UIDevice.current.batteryLevel
        guard level >= 0 else { battery = "Unavailable"; return }
        let state = UIDevice.current.batteryState
        let suffix = state == .charging ? " · Charging" : state == .full ? " · Full" : ""
        battery = "\(Int((level * 100).rounded()))%\(suffix)"
    }

    func testInternet() {
        guard !testing else { return }
        testing = true
        result = "Checking internet access…"
        testTask = Task { [weak self] in
            let config = URLSessionConfiguration.ephemeral
            config.timeoutIntervalForRequest = 5
            config.timeoutIntervalForResource = 8
            config.requestCachePolicy = .reloadIgnoringLocalCacheData
            let session = URLSession(configuration: config)
            defer { session.invalidateAndCancel() }
            let endpoints = [
                ("https://www.msftconnecttest.com/connecttest.txt", "Microsoft Connect Test"),
                ("https://www.apple.com/library/test/success.html", "<TITLE>Success</TITLE>")
            ]
            var online = false
            for (address, expected) in endpoints {
                if Task.isCancelled { return }
                guard let url = URL(string: address) else { continue }
                do {
                    var request = URLRequest(url: url)
                    request.setValue("no-cache, no-store", forHTTPHeaderField: "Cache-Control")
                    let (data, response) = try await session.data(for: request)
                    guard let http = response as? HTTPURLResponse,
                          http.statusCode == 200, http.url?.host == url.host,
                          data.count < 4096, let body = String(data: data, encoding: .utf8) else { continue }
                    online = expected == "Microsoft Connect Test" ?
                        body.trimmingCharacters(in: .whitespacesAndNewlines) == expected :
                        body.localizedCaseInsensitiveContains(expected)
                    if online { break }
                } catch { /* A second independent endpoint distinguishes a service failure. */ }
            }
            guard !Task.isCancelled, let self else { return }
            self.testing = false
            self.checkedAt = Date()
            self.result = online ? "This iPhone can reach the internet." :
                "Internet could not be verified. Check cellular service, your data plan, VPN and network settings."
        }
    }
}
