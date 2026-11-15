using TetherGuard.Core;

int count = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new Exception("FAIL: " + label);
    Console.WriteLine("PASS: " + label); count++;
}
var now = DateTimeOffset.Parse("2026-11-15T12:00:00Z");
var p = new RecoveryPolicy();
Check(!p.Observe(false, now), "Stopped policy never requests recovery");
p.Start();
Check(!p.Observe(false, now), "First failure does not reconnect");
Check(p.OutageSince == now, "Outage begins at first failed observation");
Check(p.Observe(false, now.AddSeconds(15)), "Second consecutive failure requests recovery");
p.RecordAttempt(now.AddSeconds(15));
Check(p.Attempts == 1 && p.NextAttempt == now.AddSeconds(30), "First retry delay is 15 seconds");
Check(!p.Observe(false, now.AddSeconds(29)), "Early events cannot bypass backoff");
Check(p.Observe(false, now.AddSeconds(30)), "Retry is allowed at its deadline");
p.RecordAttempt(now.AddSeconds(30));
Check(p.NextAttempt == now.AddSeconds(60), "Second delay is 30 seconds");
p.RecordAttempt(now.AddSeconds(60));
Check(p.NextAttempt == now.AddSeconds(120), "Third delay is 60 seconds");
for (int i = 0; i < 100; i++) p.RecordAttempt(now);
Check(p.NextAttempt == now.AddSeconds(60), "Backoff remains bounded after many attempts");
Check(!p.Observe(true, now.AddMinutes(5)), "Successful probe does not request recovery");
Check(p.Attempts == 0 && p.Failures == 0 && p.OutageSince is null && p.NextAttempt is null, "Success clears outage and retry state");
Check(!p.Observe(false, now.AddMinutes(6)), "A new outage requires two failures again");
p.Stop();
Check(!p.Observe(false, now.AddMinutes(7)), "Stop blocks further recovery");
var attempts = p.Attempts;
p.RecordAttempt(now);
Check(p.Attempts == attempts, "Stop blocks attempt bookkeeping");
p.Start();
Check(p.Failures == 0 && p.OutageSince is null, "Restart clears stale outage state");
var sampler = new TrafficSampler();
Check(sampler.Sample(1_000_000, 500_000, TimeSpan.FromSeconds(1)) == new TrafficRate(0, 0), "First traffic sample establishes a baseline");
Check(sampler.Sample(3_000_000, 1_500_000, TimeSpan.FromSeconds(3)) == new TrafficRate(8, 4), "Traffic uses elapsed time and converts bytes to megabits");
Check(sampler.Sample(10, 5, TimeSpan.FromSeconds(4)) == new TrafficRate(0, 0), "Adapter counter reset does not produce negative traffic");
Check(sampler.Sample(11, 6, TimeSpan.FromSeconds(4)) == new TrafficRate(0, 0), "Zero elapsed time does not divide by zero");
sampler.Reset();
Check(sampler.Sample(9_000_000, 7_000_000, TimeSpan.FromSeconds(8)) == new TrafficRate(0, 0), "Reconnection resets the traffic baseline");
Console.WriteLine($"{count} recovery and USB traffic checks passed.");
