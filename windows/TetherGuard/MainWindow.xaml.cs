using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TetherGuard.Core;
using Forms = System.Windows.Forms;

namespace TetherGuard;

public partial class MainWindow : Window
{
    private readonly RecoveryPolicy policy = new();
    private readonly TrafficSampler traffic = new();
    private readonly Stopwatch sessionClock = new();
    private readonly SessionPower sessionPower = new();
    private Adapter? sessionUsb;
    private readonly ObservableCollection<string> activity = new();
    private readonly SemaphoreSlim networkSignal = new(0, 1);
    private readonly Forms.NotifyIcon tray;
    private readonly DispatcherTimer clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private CancellationTokenSource? cancellation;
    private Task? monitoring;
    private Wlan? wlan;
    private bool exiting;
    private bool stopping;
    private bool exitRequested;
    private readonly bool smoke;
    private DateTimeOffset? lastCheck;
    private string lastState = "";
    private string? wlanError;

    public MainWindow()
    {
        InitializeComponent();
        smoke = Environment.GetCommandLineArgs().Contains("--smoke-test");
        ActivityList.ItemsSource = activity;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open TetherGuard", null, (_, _) => Dispatcher.Invoke(ShowWindow));
        menu.Items.Add("Stop monitoring", null, (_, _) => Dispatcher.InvokeAsync(StopAsync));
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.InvokeAsync(ExitAsync));
        tray = new Forms.NotifyIcon { Text = "TetherGuard — stopped", Icon = System.Drawing.SystemIcons.Shield, ContextMenuStrip = menu, Visible = !smoke };
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowWindow);
        RefreshDevices(Storage.Load());
        Closing += OnClosing;
        NetworkChange.NetworkAddressChanged += NetworkChanged;
        NetworkChange.NetworkAvailabilityChanged += AvailabilityChanged;
        clock.Tick += (_, _) => { UpdateOutage(); UpdateTraffic(); };
        clock.Start();
        if (smoke) Loaded += SmokeLoaded;
    }

    private void ShowWindow() { Show(); WindowState = WindowState.Normal; Activate(); }
    private void Log(string message)
    {
        activity.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}");
        while (activity.Count > 80) activity.RemoveAt(activity.Count - 1);
        Storage.Log(message);
    }
    private void RefreshDevices(Settings saved)
    {
        try
        {
            UsbCombo.ItemsSource = Connectivity.Adapters().OrderByDescending(a => a.Label.Contains("Apple", StringComparison.OrdinalIgnoreCase)).ThenBy(a => a.Label);
            var adapters = UsbCombo.Items.Cast<Adapter>().ToArray();
            var apple = adapters.Where(a => a.Label.Contains("Apple Mobile Device", StringComparison.OrdinalIgnoreCase)).ToArray();
            UsbCombo.SelectedItem = adapters.FirstOrDefault(a => a.Id == saved.UsbId) ?? (apple.Length == 1 ? apple[0] : null);
            wlan?.Dispose();
            wlan = null;
            wlanError = null;
            try
            {
                wlan = new Wlan();
                WifiCombo.ItemsSource = wlan.Profiles();
                // Cable-only is the default on every launch. Fallback requires explicit selection.
                WifiCombo.SelectedItem = null;
            }
            catch (Win32Exception e)
            {
                WifiCombo.ItemsSource = Array.Empty<WifiProfile>();
                wlanError = e.Message;
            }
            SetupText.Text = wlanError is null
                ? "Select the adapter identified as Apple/iPhone USB. If it is absent, connect and trust your iPhone, then refresh. Wi-Fi fallback is optional until you save a hotspot profile."
                : "Wi-Fi recovery unavailable: " + wlanError + ". Check the WLAN AutoConfig service, Wi-Fi adapter and Windows permissions. USB monitoring is still available.";
        }
        catch (Exception e) when (e is NetworkInformationException or InvalidOperationException)
        { SetupText.Text = "Could not enumerate adapters: " + e.Message; }
    }
    private Settings Selection() => new((UsbCombo.SelectedItem as Adapter)?.Id ?? "", (WifiCombo.SelectedItem as WifiProfile)?.InterfaceId.ToString() ?? "", (WifiCombo.SelectedItem as WifiProfile)?.Name ?? "");
    private void RefreshClick(object sender, RoutedEventArgs e) => RefreshDevices(Selection());

    private async void StartClick(object sender, RoutedEventArgs e)
    {
        if (monitoring is not null || stopping) return;
        var usb = UsbCombo.SelectedItem as Adapter;
        var wifi = WifiCombo.SelectedItem as WifiProfile;
        if (usb is null)
        {
            SetupText.Text = "Connect and trust your iPhone, refresh devices, and select its USB network adapter before starting.";
            return;
        }
        try { if (!smoke) Storage.Save(Selection()); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { SetupText.Text = "Settings could not be saved: " + error.Message; return; }
        UsbCombo.IsEnabled = WifiCombo.IsEnabled = RefreshButton.IsEnabled = StartButton.IsEnabled = AwakeCheck.IsEnabled = false;
        StopButton.IsEnabled = true;
        cancellation = new();
        policy.Start();
        sessionUsb = usb;
        traffic.Reset();
        sessionClock.Restart();
        if (!smoke && AwakeCheck.IsChecked == true)
            Log(sessionPower.Acquire() ? "Requested protection from automatic PC sleep for this session." : "Windows could not apply the keep-awake request.");
        lastCheck = null;
        lastState = "";
        StatusText.Text = "Checking connection…";
        Log("Monitoring started. Automatic fallback is " + (wifi is null ? "unconfigured." : "enabled for the selected saved profile."));
        monitoring = MonitorAsync(usb, wifi, cancellation.Token);
        await Task.Yield();
    }

    private async Task MonitorAsync(Adapter? usb, WifiProfile? wifi, CancellationToken token)
    {
        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                // Debounce bursts of network-change notifications without overlapping checks.
                if (lastCheck is { } previous && DateTimeOffset.UtcNow - previous < TimeSpan.FromSeconds(3))
                    await Task.Delay(TimeSpan.FromSeconds(3) - (DateTimeOffset.UtcNow - previous), token);
                var result = await Connectivity.ProbeAsync(token);
                token.ThrowIfCancellationRequested();
                var now = DateTimeOffset.UtcNow;
                lastCheck = now;
                bool recover = policy.Observe(result.Online, now);
                string state;
                if (result.Online)
                {
                    bool viaUsb = usb is not null && SameId(result.AdapterId, usb.Id);
                    bool viaWifi = wifi is not null && SameId(result.AdapterId, wifi.InterfaceId.ToString()) && wlan?.CurrentProfile(wifi.InterfaceId) == wifi.Name;
                    state = viaUsb ? "Online · iPhone USB" : viaWifi ? "Online · hotspot Wi-Fi" : "Online · another route";
                    DetailText.Text = result.Detail + " · " + DateTime.Now.ToString("HH:mm:ss") + (viaUsb || viaWifi ? "" : ". iPhone tethering is not verified on this route.");
                }
                else
                {
                    state = policy.Failures < 2 ? "Checking a possible outage…" : "Offline · recovery needed";
                    DetailText.Text = result.Detail + " Reconnect USB and tap Trust, or enable Allow Others to Join on iPhone.";
                }
                StatusText.Text = state;
                RouteText.Text = result.Route;
                if (state != lastState)
                {
                    Log(state);
                    if (!smoke && !result.Online && policy.Failures >= 2)
                        tray.ShowBalloonTip(6000, "TetherGuard: internet check failed", "Attempting your saved Wi-Fi fallback if configured. Check USB, Allow Others to Join and cellular service.", Forms.ToolTipIcon.Warning);
                    lastState = state;
                }
                tray.Text = result.Online ? "TetherGuard — online" : "TetherGuard — checking / offline";
                if (recover && wifi is not null && wlan is not null)
                {
                    token.ThrowIfCancellationRequested();
                    policy.RecordAttempt(now);
                    try
                    {
                        // Runs synchronously on the UI thread: Stop cannot interleave after the token
                        // check and before this request. Previously accepted OS requests may complete.
                        wlan.Connect(wifi);
                        Log($"Wi-Fi recovery request {policy.Attempts} accepted by Windows; verifying on the next check.");
                    }
                    catch (Win32Exception e)
                    {
                        Log($"Wi-Fi recovery request {policy.Attempts} failed (Windows error {e.NativeErrorCode}).");
                        DetailText.Text = "Wi-Fi recovery: " + e.Message + ". Check Wi-Fi, the saved profile and the iPhone hotspot. Reconnect USB if available.";
                    }
                }
                UpdateOutage();
                await networkSignal.WaitAsync(TimeSpan.FromSeconds(15), token);
                while (networkSignal.Wait(0)) { }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception e)
        {
            policy.Stop();
            StatusText.Text = "Monitoring stopped after an error";
            DetailText.Text = e.Message;
            Log("Monitoring stopped after an unexpected error: " + e.GetType().Name);
            // Schedule cleanup after this task has completed to avoid waiting on ourselves.
            _ = Dispatcher.InvokeAsync(async () => await StopAsync());
        }
    }

    private static bool SameId(string? a, string b) => Guid.TryParse(a, out var first) && Guid.TryParse(b, out var second) && first == second;
    private void UpdateOutage()
    {
        OutageText.Text = policy.OutageSince is { } since
            ? $"Outage: {(DateTimeOffset.UtcNow - since):hh\\:mm\\:ss} · recovery requests: {policy.Attempts}" + (policy.Running ? "" : " · monitoring stopped")
            : "No current recorded outage";
    }
    private void SignalNetwork()
    {
        try { networkSignal.Release(); } catch (SemaphoreFullException) { }
    }
    private void NetworkChanged(object? sender, EventArgs e) => SignalNetwork();
    private void AvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e) => SignalNetwork();
    private void UpdateTraffic()
    {
        if (sessionUsb is null || !policy.Running) return;
        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => SameId(n.Id, sessionUsb.Id));
            if (nic is null || nic.OperationalStatus != OperationalStatus.Up)
            {
                traffic.Reset();
                TrafficText.Text = "USB adapter disconnected. Reconnect the cable and check Trust.";
                return;
            }
            var counters = nic.GetIPStatistics();
            var rate = traffic.Sample(counters.BytesReceived, counters.BytesSent, sessionClock.Elapsed);
            TrafficText.Text = $"USB traffic  ↓ {rate.DownloadMbps:F2} Mbps   ↑ {rate.UploadMbps:F2} Mbps";
        }
        catch (Exception e) when (e is NetworkInformationException or InvalidOperationException)
        {
            traffic.Reset();
            TrafficText.Text = "USB traffic counters are unavailable.";
        }
    }
    private async void StopClick(object sender, RoutedEventArgs e) => await StopAsync();
    private async Task StopAsync()
    {
        if (stopping)
        {
            if (monitoring is not null) await monitoring;
            return;
        }
        stopping = true;
        try
        {
            cancellation?.Cancel();
            policy.Stop();
            if (monitoring is not null) await monitoring;
            monitoring = null;
            cancellation?.Dispose();
            cancellation = null;
            sessionPower.Dispose();
            sessionClock.Stop();
            sessionUsb = null;
            TrafficText.Text = "USB traffic monitoring stopped.";
            UsbCombo.IsEnabled = WifiCombo.IsEnabled = RefreshButton.IsEnabled = StartButton.IsEnabled = AwakeCheck.IsEnabled = true;
            StopButton.IsEnabled = false;
            StatusText.Text = "Monitoring stopped";
            DetailText.Text = "No further checks or recovery requests will be sent. A Wi-Fi request already accepted by Windows can still finish.";
            tray.Text = "TetherGuard — stopped";
            Log("Monitoring stopped.");
        }
        finally { stopping = false; }
    }
    private void LogsClick(object sender, RoutedEventArgs e)
    {
        try { Directory.CreateDirectory(Storage.Logs); Process.Start(new ProcessStartInfo(Storage.Logs) { UseShellExecute = true }); }
        catch (Exception error) { SetupText.Text = "Could not open logs: " + error.Message; }
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!exiting) { e.Cancel = true; Hide(); }
    }
    private async Task ExitAsync()
    {
        if (exitRequested) return;
        exitRequested = true;
        await StopAsync();
        exiting = true;
        clock.Stop();
        NetworkChange.NetworkAddressChanged -= NetworkChanged;
        NetworkChange.NetworkAvailabilityChanged -= AvailabilityChanged;
        wlan?.Dispose();
        tray.Dispose();
        Close();
    }
    private async void SmokeLoaded(object sender, RoutedEventArgs e)
    {
        // Read-only integration check: layout and native enumeration; never calls WlanConnect.
        try
        {
            await Task.Delay(500);
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "--smoke-test");
            var folder = args[index + 1];
            Directory.CreateDirectory(folder);
            var image = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
            image.Render(this);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using (var file = File.Create(Path.Combine(folder, "windows-preview.png"))) encoder.Save(file);
            var probe = await Connectivity.ProbeAsync(CancellationToken.None);
            using (var powerCheck = new SessionPower())
                if (!powerCheck.Acquire()) throw new InvalidOperationException("Windows rejected the temporary keep-awake test request.");
            string lifecycle = "not run: no adapter";
            string trafficCheck = "not run";
            if (UsbCombo.Items.Count > 0)
            {
                UsbCombo.SelectedItem = UsbCombo.Items.Cast<Adapter>().FirstOrDefault(a => SameId(a.Id, probe.AdapterId ?? "")) ?? UsbCombo.Items[0];
                WifiCombo.SelectedItem = null; // Never issue WlanConnect in a smoke test.
                StartClick(this, new RoutedEventArgs());
                var activeTask = monitoring;
                StartClick(this, new RoutedEventArgs());
                if (!ReferenceEquals(activeTask, monitoring)) throw new InvalidOperationException("Duplicate Start created another loop.");
                for (int i = 0; i < 150 && lastCheck is null; i++) await Task.Delay(100);
                if (lastCheck is null) throw new InvalidOperationException("Monitor did not finish its first check.");
                UpdateTraffic();
                trafficCheck = TrafficText.Text;
                if (!trafficCheck.StartsWith("USB traffic") && !trafficCheck.StartsWith("USB adapter disconnected"))
                    throw new InvalidOperationException("USB traffic counters could not be read.");
                await StopAsync();
                var stoppedAt = lastCheck;
                SignalNetwork();
                await Task.Delay(3500);
                if (lastCheck != stoppedAt || monitoring is not null || policy.Running || policy.Attempts != 0)
                    throw new InvalidOperationException("Stop did not quiesce monitoring.");
                lifecycle = "passed: Start, duplicate Start, initial probe, Stop, network event after Stop; no further checks";
            }
            File.WriteAllText(Path.Combine(folder, "smoke-test.txt"), $"WPF layout: rendered\nNetwork adapters: {UsbCombo.Items.Count}\nSaved profiles: {WifiCombo.Items.Count}\nWLAN: {wlanError ?? "available"}\nInternet verified: {probe.Online}\nActual outbound adapter: {probe.Route}\nUSB counters: {trafficCheck}\nKeep-awake API: temporary request accepted and disposed\nLifecycle: {lifecycle}\nNo recovery requests were issued.\n");
        }
        catch (Exception error) { Storage.Log("Smoke test failed: " + error); Environment.ExitCode = 1; }
        await ExitAsync();
    }
}
