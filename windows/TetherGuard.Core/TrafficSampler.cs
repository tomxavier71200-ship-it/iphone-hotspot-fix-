namespace TetherGuard.Core;

public readonly record struct TrafficRate(double DownloadMbps, double UploadMbps);
public sealed class TrafficSampler
{
    private long? received, sent;
    private TimeSpan previous;
    public TrafficRate Sample(long bytesReceived, long bytesSent, TimeSpan elapsed)
    {
        var seconds = (elapsed - previous).TotalSeconds;
        var rate = received is { } rx && sent is { } tx && seconds > 0 && bytesReceived >= rx && bytesSent >= tx
            ? new TrafficRate((bytesReceived - rx) * 8.0 / seconds / 1_000_000, (bytesSent - tx) * 8.0 / seconds / 1_000_000)
            : new TrafficRate(0, 0);
        received = bytesReceived; sent = bytesSent; previous = elapsed;
        return rate;
    }
    public void Reset() { received = sent = null; previous = TimeSpan.Zero; }
}
