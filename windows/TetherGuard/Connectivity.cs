using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TetherGuard;

public sealed record Adapter(string Id, string Label);
public sealed record ProbeResult(bool Online, string? AdapterId, string Route, string Detail);
public static class Connectivity
{
    public static Adapter[] Adapters() => NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
        .Select(n => new Adapter(n.Id, $"{n.Name} — {n.Description}")).ToArray();

    public static async Task<ProbeResult> ProbeAsync(CancellationToken token)
    {
        // Sequential fallback avoids depending on a single test service. No proxy: route attribution
        // refers to the outbound socket, not an HTTP proxy. VPNs are reported as their own adapter.
        foreach (var (url, expected) in new[] {
            ("https://www.msftconnecttest.com/connecttest.txt", "Microsoft Connect Test"),
            ("https://www.apple.com/library/test/success.html", "<TITLE>Success</TITLE>") })
        {
            token.ThrowIfCancellationRequested();
            string? adapterId = null;
            string route = "Route unavailable";
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            using var handler = new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false };
            handler.ConnectCallback = async (context, ct) =>
            {
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(context.DnsEndPoint, ct);
                    if (socket.LocalEndPoint is IPEndPoint local)
                    {
                        var nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n =>
                            n.GetIPProperties().UnicastAddresses.Any(a => a.Address.Equals(local.Address)));
                        adapterId = nic?.Id;
                        route = nic is null ? "Route unavailable" : $"{nic.Name} — {nic.Description}";
                    }
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch { socket.Dispose(); throw; }
            };
            using var client = new HttpClient(handler);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.CacheControl = new() { NoCache = true, NoStore = true };
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if (response.StatusCode != HttpStatusCode.OK) continue;
                // Bound response memory even if a captive portal or middlebox returns a large page.
                using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
                var buffer = new byte[4096];
                int length = 0, read;
                while (length < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(length), timeout.Token)) > 0) length += read;
                var body = System.Text.Encoding.UTF8.GetString(buffer, 0, length);
                bool valid = expected == "Microsoft Connect Test" ? body.Trim() == expected : body.Contains(expected, StringComparison.OrdinalIgnoreCase);
                if (valid) return new(true, adapterId, route, "HTTPS internet check passed");
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
            catch (Exception e) when (e is HttpRequestException or SocketException or IOException) { }
        }
        token.ThrowIfCancellationRequested();
        return new(false, null, "No verified internet route", "Both HTTPS checks failed. Cellular service, DNS, VPN or a firewall may also be the cause.");
    }
}
