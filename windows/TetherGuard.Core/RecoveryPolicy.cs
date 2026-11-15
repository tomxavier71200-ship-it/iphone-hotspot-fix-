namespace TetherGuard.Core;

// Time is supplied by the caller so retry behavior can be tested without sleeping.
public sealed class RecoveryPolicy
{
    public bool Running { get; private set; }
    public int Failures { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset? OutageSince { get; private set; }
    public DateTimeOffset? NextAttempt { get; private set; }

    public void Start()
    {
        Running = true;
        Failures = Attempts = 0;
        OutageSince = NextAttempt = null;
    }

    public void Stop() => Running = false;

    public bool Observe(bool online, DateTimeOffset now)
    {
        if (!Running) return false;
        if (online)
        {
            Failures = Attempts = 0;
            OutageSince = NextAttempt = null;
            return false;
        }
        Failures++;
        OutageSince ??= now;
        return Failures >= 2 && (NextAttempt is null || now >= NextAttempt);
    }

    public void RecordAttempt(DateTimeOffset now)
    {
        if (!Running) return;
        Attempts++;
        NextAttempt = now.AddSeconds(Math.Min(60, 15 * Math.Pow(2, Math.Min(Attempts - 1, 3))));
    }
}
