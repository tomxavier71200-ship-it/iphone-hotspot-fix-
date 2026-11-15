# Device acceptance checklist

These are manual tests. They have not been performed unless explicitly recorded in `validation/RESULTS.md`. Do not run disruptive tests during important calls/downloads.

1. **USB, locked phone:** select the actual Apple USB adapter, enable monitoring and lock iPhone. Confirm a successful check reports iPhone USB. Leave idle for 30 minutes, then browse/download and review outage logs.
2. **USB removal with Wi-Fi fallback:** save and select the hotspot profile; unplug USB while internet is otherwise unavailable. After two failed checks, confirm a connection request and verified hotspot Wi-Fi status if iOS advertises the hotspot. Record recovery time and any interrupted session.
3. **USB return:** reconnect and trust iPhone. Confirm the status accurately reflects the adapter Windows actually uses; do not assume Windows immediately switches back to USB.
4. **Hotspot unavailable:** disable Personal Hotspot with USB disconnected. Confirm repeated failures, manual guidance and bounded Wi-Fi attempts. Restore the hotspot and confirm successful checks clear the current outage and retry state.
5. **Cellular loss:** with Wi-Fi hotspot still connected, disable cellular data temporarily. Confirm link presence is not reported as working internet. Restore cellular data and confirm recovery.
6. **Stale credentials/profile:** change the hotspot password and confirm failed attempts never appear as verified connectivity. Rejoin using Windows, refresh the selection and confirm recovery. Test removing the saved profile while monitoring.
7. **Stop during outage:** stop while retrying. Confirm no new requests or probes are logged; an OS request already submitted may finish. Exit the app and verify the tray icon disappears.
8. **Sleep/resume and adapter changes:** suspend Windows, resume, unplug/replug Wi-Fi and USB adapters, then confirm checks resume without duplicate loops. Click Start twice rapidly and confirm only one monitor runs.
9. **Other internet and VPN:** connect a working non-iPhone network or VPN. Confirm it is reported as another route and not as iPhone USB. Test endpoint-blocking firewall/proxy configurations and document limitations.
10. **iPhone build and UI:** compile the shared scheme on Mac, install on a physical iPhone, inspect light/dark mode, large Dynamic Type, VoiceOver, landscape, battery changes and cellular/Wi-Fi transitions. Start a manual test, background the app, and confirm it cancels and can be rerun after reopening.

Acceptance requires truthful route/status reporting, bounded serialized recovery, working Stop/Exit, and no claim that the iPhone app can control Personal Hotspot. Zero downtime is not an acceptance criterion because the OS and cellular network can interrupt service.
