# Build and test

## Windows

Prerequisites: Windows 11, .NET 10 SDK. No external NuGet packages are used. Run from `source`:

```powershell
dotnet build windows/TetherGuard/TetherGuard.csproj -c Release
dotnet run --project windows/TetherGuard.Tests/TetherGuard.Tests.csproj -c Release
dotnet publish windows/TetherGuard/TetherGuard.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o ../Windows
```

Read-only integration test (renders the window, enumerates adapters/profiles, probes current internet, starts/stops monitoring without a Wi-Fi fallback, and exits):

```powershell
./../Windows/TetherGuard.exe --smoke-test "$env:TEMP/TetherGuard-smoke"
```

This writes `windows-preview.png` and `smoke-test.txt`. Wait for the process to exit, then inspect the report. It does not issue Wi-Fi connection requests or save selected profiles. Do not confuse this with a hardware failover test. The executable extracts native runtime files to the normal .NET extraction location as needed.

## iPhone

On a Mac, install Xcode 16 or newer. Open `iphone/TetherGuard.xcodeproj`, choose a signing team, and use a unique bundle identifier for device installation. The shared scheme is `TetherGuard`.

Unsigned simulator compilation:

```sh
xcodebuild -project iphone/TetherGuard.xcodeproj -scheme TetherGuard -configuration Debug -sdk iphonesimulator -destination 'generic/platform=iOS Simulator' CODE_SIGNING_ALLOWED=NO build
```

Use a physical iPhone for battery readings, cellular and tethering validation. Network and SwiftUI Apple frameworks cannot be compiled or exercised in this Windows environment.

## Architecture

`TetherGuard.Core` contains a clock-driven recovery policy with no platform dependencies. `TetherGuard.Tests` exercises failures, retries, recovery, Stop and restart without sleeping or network mutation.

The WPF app owns a single cancelable monitor loop. Network-change events wake that loop; they do not create parallel checks. Stop cancels and awaits its completion. A local named mutex prevents duplicate interactive instances. USB traffic is sampled from adapter counters using monotonic elapsed time, with baselines reset after disconnects/counter resets. The visible keep-awake option holds a UI-thread `SetThreadExecutionState` system request only during the session, releasing it on Stop/Exit; it does not keep the screen on or prevent manual sleep.

Internet verification uses a new direct socket for each test, checks the HTTPS response, and maps the successful socket's actual local IP to a network adapter. It does not infer internet availability from link status. WLAN interop enumerates saved profiles and calls `WlanConnect` in profile mode; a successful return means only that Windows accepted the request. A later probe establishes internet access. No credentials are read.

The SwiftUI app uses `NWPathMonitor`, battery notifications and foreground manual `URLSession` tests. It has no dependency on the Windows helper and exposes no server or remote-control API. The privacy manifest declares no tracking, collected app data or required-reason API use.
