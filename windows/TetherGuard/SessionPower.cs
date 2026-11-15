using System;
using System.Runtime.InteropServices;

namespace TetherGuard;

// All calls are on the WPF UI thread; this requirement belongs to that thread.
public sealed class SessionPower : IDisposable
{
    private bool held;
    public bool Acquire()
    {
        held = SetThreadExecutionState(0x80000001) != 0; // continuous + system required; display may sleep
        return held;
    }
    public void Dispose()
    {
        if (held) SetThreadExecutionState(0x80000000);
        held = false;
    }
    [DllImport("kernel32.dll")] private static extern uint SetThreadExecutionState(uint flags);
}
