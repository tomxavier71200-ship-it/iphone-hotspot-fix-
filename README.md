# TetherGuard · iPhone hotspot companion

Custom Windows USB tethering monitor and native iPhone companion. Plug iPhone into your PC, verify internet is using the USB adapter, watch live traffic and protect the session from automatic Windows sleep.

**An ordinary iPhone app cannot force Personal Hotspot to stay enabled or guarantee uninterrupted internet.** This project provides diagnostics and best-effort reconnection. Connection switches can interrupt calls and downloads.


## Add to LiveContainer Sources

In LiveContainer, open **Sources → +** and paste this catalog URL:

```text
https://raw.githubusercontent.com/tomxavier71200-ship-it/iphone-hotspot-fix-/main/source.json
```

The catalog downloads the IPA directly from the public v1.0 release, without a GitHub login or an expiring Actions artifact link. iOS 17+ is required. The JSON format and IPA download have been validated; running inside LiveContainer has not been tested on a physical iPhone. The iPhone app is a diagnostic companion; it cannot control Personal Hotspot or boost cellular speed.

## Download builds from GitHub

1. Open this repository's **Actions** tab and select the latest successful **Build TetherGuard** run.
2. Scroll to **Artifacts**. Sign in to GitHub to download them.
3. **TetherGuard-Windows-x64** contains a self-contained Windows 11 executable. Extract and run it; no .NET runtime installation is required.
4. **TetherGuard-iPhone-unsigned** contains `TetherGuard-unsigned.ipa`. Extract it from the artifact ZIP. **It is unsigned and cannot be installed directly.** Sign it with your provisioning setup or use a compatible signing/sideloading tool. Never commit signing certificates, provisioning credentials or Apple account passwords.

GitHub builds the iPhone app using Xcode on a hosted macOS runner, so you do not need your own Mac for compilation. Installing on a real iPhone still requires signing. No Apple signing credentials or paid services are configured by this workflow. Download artifacts within 14 days or rerun the workflow using **Run workflow**.

## Use the Windows helper

1. Install/update **Apple Devices** on Windows. On iPhone, enable **Settings → Personal Hotspot → Allow Others to Join**.
2. Connect a USB cable that supports data, unlock iPhone and tap **Trust** if prompted.
3. In TetherGuard, select the Apple/iPhone USB network adapter. Keep **Keep this PC awake while monitoring** checked if wanted, then start monitoring.
4. USB-only is the default. Leave **Optional Wi-Fi fallback** unselected. If a different connection or VPN is reported, disconnect competing networks yourself to verify that your PC is using iPhone USB.
5. Close the window to keep it in the tray. Use the tray menu to reopen, stop or exit.

The iPhone companion is optional for Windows recovery. It shows phone network/battery diagnostics, a manual internet check and setup guidance. It does not need to remain open and cannot control Personal Hotspot or verify Windows internet access.

## Behavior and privacy

- Verifies HTTPS response content; identifies the adapter used by the successful test. A live adapter alone is not reported as working internet.
- Shows live download/upload traffic from the selected USB adapter. This measures current activity, not maximum available bandwidth. It does not run large downloads or speed tests automatically.
- The visible keep-awake option requests protection from automatic system sleep during monitoring and releases it on Stop/Exit. The display may turn off; manual sleep and laptop-lid actions still apply.
- Wi-Fi fallback is optional and unselected on every launch. Only if explicitly configured, two failed checks trigger requests with 15/30/60-second backoff. Windows accepting a request is not proof of a successful connection.
- Windows controls routing. The helper does not change adapter priorities or reset adapters. A working other network/VPN is reported as another route; recovery is not attempted while internet already works.
- Stop cancels monitoring, releases the keep-awake request and prevents new requests; a request already accepted by Windows may finish.
- Checks contact Microsoft and, if necessary, Apple. A blocked endpoint, proxy, DNS issue, VPN or firewall can cause checks to fail. The helper checks after 15-second waits and network events; failed checks can take additional time.
- Settings and logs stay in `%LOCALAPPDATA%\TetherGuard`. Logs exclude passwords, profile names and browsing history, with a 1 MB daily cap and seven-day retention. Windows retains saved Wi-Fi credentials. No accounts, pairing server or analytics.

## Build and validation

See [BUILD.md](BUILD.md) for local commands and architecture, and [HARDWARE-TESTS.md](HARDWARE-TESTS.md) for physical-device acceptance tests.

The Windows Release build, 21 recovery/USB-traffic checks, interface rendering, native enumeration and Start/Stop smoke test are covered by local validation. GitHub Actions builds both apps, and has successfully compiled the iPhone app on macOS. The initial live connectivity probe used an Apple Mobile Device Ethernet adapter. Long idle sessions and optional Wi-Fi failover still need physical-device testing; a green build does not verify device behavior.

## Getting the best available USB connection

Use a reliable data-capable cable and a direct PC port. Keep iPhone cool and in a position with good cellular reception. Check your carrier's tethering allowance and any hotspot speed cap. If Windows reports another route, check competing networks/VPNs. None of these apps can increase cellular capacity or remove carrier limits. The helper's useful work is verifying the USB route, showing actual traffic, recording interruptions and preventing automatic PC sleep when requested.

Sources: [Apple USB tethering](https://support.apple.com/en-ca/111785), [Personal Hotspot API limitations](https://developer.apple.com/forums/thread/113933), [Windows WLAN API](https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlanconnect).
