# TetherGuard · iPhone hotspot companion

Custom Windows tethering monitor and native iPhone companion. USB is the intended primary connection; the Windows helper attempts a saved iPhone hotspot Wi-Fi profile after internet checks fail.

**An ordinary iPhone app cannot force Personal Hotspot to stay enabled or guarantee uninterrupted internet.** This project provides diagnostics and best-effort reconnection. Connection switches can interrupt calls and downloads.

## Download builds from GitHub

1. Open this repository's **Actions** tab and select the latest successful **Build TetherGuard** run.
2. Scroll to **Artifacts**. Sign in to GitHub to download them.
3. **TetherGuard-Windows-x64** contains a self-contained Windows 11 executable. Extract and run it; no .NET runtime installation is required.
4. **TetherGuard-iPhone-unsigned** contains `TetherGuard-unsigned.ipa`. Extract it from the artifact ZIP. **It is unsigned and cannot be installed directly.** Sign it with your provisioning setup or use a compatible signing/sideloading tool. Never commit signing certificates, provisioning credentials or Apple account passwords.

GitHub builds the iPhone app using Xcode on a hosted macOS runner, so you do not need your own Mac for compilation. Installing on a real iPhone still requires signing. No Apple signing credentials or paid services are configured by this workflow. Download artifacts within 14 days or rerun the workflow using **Run workflow**.

## Use the Windows helper

1. Install/update **Apple Devices** on Windows. On iPhone, enable **Settings → Personal Hotspot → Allow Others to Join**.
2. Connect a USB cable that supports data, unlock iPhone and tap **Trust** if prompted.
3. Join the iPhone's hotspot once through Windows Wi-Fi to save its profile.
4. In TetherGuard, select the Apple/iPhone USB network adapter and your saved hotspot Wi-Fi profile. Start monitoring.
5. Close the window to keep it in the tray. Use the tray menu to reopen, stop or exit.

The iPhone companion is optional for Windows recovery. It shows phone network/battery diagnostics, a manual internet check and setup guidance. It does not need to remain open and cannot control Personal Hotspot or verify Windows internet access.

## Behavior and privacy

- Verifies HTTPS response content; identifies the adapter used by the successful test. A live adapter alone is not reported as working internet.
- After two consecutive failed checks, requests the selected saved Wi-Fi profile with 15/30/60-second backoff, capped at 60 seconds. A request accepted by Windows is not proof of a successful connection.
- Windows controls routing. The helper does not change adapter priorities or reset adapters. A working other network/VPN is reported as another route; recovery is not attempted while internet already works.
- Stop cancels monitoring and prevents new requests; a request already accepted by Windows may finish. Monitoring requires an awake Windows PC.
- Checks contact Microsoft and, if necessary, Apple. A blocked endpoint, proxy, DNS issue, VPN or firewall can cause checks to fail. The helper checks after 15-second waits and network events; failed checks can take additional time.
- Settings and logs stay in `%LOCALAPPDATA%\TetherGuard`. Logs exclude passwords, profile names and browsing history, with a 1 MB daily cap and seven-day retention. Windows retains saved Wi-Fi credentials. No accounts, pairing server or analytics.

## Build and validation

See [BUILD.md](BUILD.md) for local commands and architecture, and [HARDWARE-TESTS.md](HARDWARE-TESTS.md) for physical-device acceptance tests.

The Windows Release build, 16 recovery-policy checks, interface rendering, native enumeration and Start/Stop smoke test passed locally. The initial live connectivity probe used an Apple Mobile Device Ethernet adapter. Automatic Wi-Fi failover and long idle sessions still need physical-device testing. GitHub Actions provides actual iPhone compilation; a green build does not verify physical-device behavior.

Sources: [Apple USB tethering](https://support.apple.com/en-ca/111785), [Personal Hotspot API limitations](https://developer.apple.com/forums/thread/113933), [Windows WLAN API](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlanconnect).
