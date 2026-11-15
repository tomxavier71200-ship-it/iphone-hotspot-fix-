using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TetherGuard;

public sealed record WifiProfile(Guid InterfaceId, string InterfaceName, string Name)
{
    public string Label => $"{Name} — {InterfaceName}";
}

// Native WLAN profile mode: Windows owns credentials; never export profile XML/key material.
public sealed class Wlan : IDisposable
{
    private IntPtr handle;
    public Wlan() => Check(WlanOpenHandle(2, IntPtr.Zero, out _, out handle));
    private static void Check(uint error) { if (error != 0) throw new Win32Exception((int)error); }
    public List<WifiProfile> Profiles()
    {
        var result = new List<WifiProfile>();
        Check(WlanEnumInterfaces(handle, IntPtr.Zero, out var list));
        try
        {
            int count = Marshal.ReadInt32(list);
            for (int i = 0; i < count; i++)
            {
                var item = Marshal.PtrToStructure<InterfaceInfo>(IntPtr.Add(list, 8 + i * Marshal.SizeOf<InterfaceInfo>()));
                var id = item.Id;
                Check(WlanGetProfileList(handle, ref id, IntPtr.Zero, out var profiles));
                try
                {
                    int n = Marshal.ReadInt32(profiles);
                    for (int j = 0; j < n; j++)
                    {
                        var profile = Marshal.PtrToStructure<ProfileInfo>(IntPtr.Add(profiles, 8 + j * Marshal.SizeOf<ProfileInfo>()));
                        result.Add(new(id, item.Description, profile.Name));
                    }
                }
                finally { WlanFreeMemory(profiles); }
            }
        }
        finally { WlanFreeMemory(list); }
        return result;
    }
    public void Connect(WifiProfile profile)
    {
        var id = profile.InterfaceId;
        var options = new ConnectionParameters { Mode = 0, Profile = profile.Name, BssType = 1 };
        Check(WlanConnect(handle, ref id, ref options, IntPtr.Zero));
        // Success means request accepted, not connected. A subsequent HTTP probe verifies internet.
    }
    public string? CurrentProfile(Guid id)
    {
        var error = WlanQueryInterface(handle, ref id, 7, IntPtr.Zero, out _, out var data, out _);
        if (error != 0) return null;
        try { return Marshal.PtrToStructure<ConnectionAttributes>(data).Profile; }
        finally { WlanFreeMemory(data); }
    }
    public void Dispose() { if (handle != IntPtr.Zero) { WlanCloseHandle(handle, IntPtr.Zero); handle = IntPtr.Zero; } }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct InterfaceInfo { public Guid Id; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Description; public int State; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProfileInfo { [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Name; public uint Flags; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ConnectionParameters { public int Mode; [MarshalAs(UnmanagedType.LPWStr)] public string Profile; public IntPtr Ssid; public IntPtr DesiredBssid; public int BssType; public uint Flags; }
    // Prefix of WLAN_CONNECTION_ATTRIBUTES. Query buffer includes the remaining association/security fields.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ConnectionAttributes { public int State; public int Mode; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Profile; }
    [DllImport("wlanapi.dll")] private static extern uint WlanOpenHandle(uint version, IntPtr reserved, out uint negotiated, out IntPtr handle);
    [DllImport("wlanapi.dll")] private static extern uint WlanCloseHandle(IntPtr handle, IntPtr reserved);
    [DllImport("wlanapi.dll")] private static extern uint WlanEnumInterfaces(IntPtr handle, IntPtr reserved, out IntPtr list);
    [DllImport("wlanapi.dll")] private static extern uint WlanGetProfileList(IntPtr handle, ref Guid id, IntPtr reserved, out IntPtr list);
    [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)] private static extern uint WlanConnect(IntPtr handle, ref Guid id, ref ConnectionParameters parameters, IntPtr reserved);
    [DllImport("wlanapi.dll")] private static extern uint WlanQueryInterface(IntPtr handle, ref Guid id, int opcode, IntPtr reserved, out uint size, out IntPtr data, out int type);
    [DllImport("wlanapi.dll")] private static extern void WlanFreeMemory(IntPtr memory);
}
