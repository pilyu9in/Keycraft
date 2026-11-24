using System.Runtime.InteropServices;
using System.Text;

namespace Keycraft;

internal static partial class Win
{
    internal const uint Keyboard = 1, Hid = 2, DeviceName = 0x20000007, DeviceInfo = 0x2000000b,
        PreparsedData = 0x20000005, Input = 0x10000003;
    internal const int MessageInput = 0xff, MessageDeviceChange = 0xfe;
    internal const uint InputSink = 0x100, DeviceNotify = 0x2000, Remove = 1;
    internal const int HidSuccess = 0x00110000;

    [StructLayout(LayoutKind.Sequential)] internal struct DeviceEntry { public nint Handle; public uint Type; }
    [StructLayout(LayoutKind.Sequential)] internal struct Registration { public ushort Page, Usage; public uint Flags; public nint Window; }
    [StructLayout(LayoutKind.Sequential)] internal struct Header { public uint Type, Size; public nint Device, Param; }
    [StructLayout(LayoutKind.Sequential)] internal struct RawKey { public ushort Scan, Flags, Reserved, VirtualKey; public uint Message, Extra; }
    [StructLayout(LayoutKind.Sequential)] internal struct HookKey { public uint Vk, Scan, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] internal struct KeyInput { public ushort Vk, Scan; public uint Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Explicit)] internal struct InputValue
    {
        [FieldOffset(0)] public KeyInput Key;
        [FieldOffset(0)] private MouseInput mouse;
    }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] internal struct InputEvent { public uint Type; public InputValue Value; }
    [StructLayout(LayoutKind.Explicit, Size = 64)] internal struct HidCaps
    {
        [FieldOffset(0)] public ushort Usage;
        [FieldOffset(2)] public ushort Page;
        [FieldOffset(4)] public ushort ReportBytes;
        [FieldOffset(46)] public ushort InputButtons;
        [FieldOffset(48)] public ushort InputValues;
    }
    [StructLayout(LayoutKind.Explicit, Size = 72)] internal struct ItemCaps
    {
        [FieldOffset(0)] public ushort Page;
        [FieldOffset(2)] public byte Report;
        [FieldOffset(12)] public byte IsRange;
        [FieldOffset(18)] public ushort BitSize;
        [FieldOffset(56)] public ushort UsageMin;
        [FieldOffset(58)] public ushort UsageMax;
        [FieldOffset(6)] public ushort LinkCollection;
        [FieldOffset(4)] public ushort BitField;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct HidUsage { public ushort Usage, Page; }

    internal delegate nint KeyboardCallback(int code, nint message, nint data);
    [DllImport("user32.dll", SetLastError=true)] internal static extern uint GetRawInputDeviceList([Out] DeviceEntry[]? entries, ref uint count, uint size);
    [DllImport("user32.dll", EntryPoint="GetRawInputDeviceInfoW", SetLastError=true)] internal static extern uint DeviceBytes(nint device, uint command, nint data, ref uint size);
    [DllImport("user32.dll", EntryPoint="GetRawInputDeviceInfoW", CharSet=CharSet.Unicode, SetLastError=true)] internal static extern uint DeviceText(nint device, uint command, StringBuilder data, ref uint size);
    [DllImport("user32.dll", SetLastError=true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool RegisterRawInputDevices(Registration[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError=true)] internal static extern uint GetRawInputData(nint input, uint command, nint data, ref uint size, uint headerSize);
    [DllImport("hid.dll")] internal static extern int HidP_GetCaps(nint data, out HidCaps caps);
    [DllImport("hid.dll")] internal static extern int HidP_GetButtonCaps(int kind, [Out] ItemCaps[] caps, ref ushort count, nint data);
    [DllImport("hid.dll")] internal static extern int HidP_GetValueCaps(int kind, [Out] ItemCaps[] caps, ref ushort count, nint data);
    [DllImport("hid.dll")] internal static extern int HidP_GetUsagesEx(int kind, ushort collection, [Out] HidUsage[] list, ref uint length, nint data, byte[] report, uint reportLength);
    [DllImport("hid.dll")] internal static extern uint HidP_MaxUsageListLength(int kind, ushort page, nint data);
    [DllImport("hid.dll")] internal static extern int HidP_GetUsageValue(int kind, ushort page, ushort collection, ushort usage, out uint value, nint data, byte[] report, uint reportLength);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] internal static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll", EntryPoint="SetWindowsHookExW", SetLastError=true)] internal static extern nint SetWindowsHookEx(int kind, KeyboardCallback callback, nint module, uint thread);
    [DllImport("user32.dll", SetLastError=true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] internal static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll", SetLastError=true)] internal static extern uint SendInput(uint count, InputEvent[] data, int size);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern int GetKeyNameText(int value, StringBuilder text, int size);
    [DllImport("user32.dll")] internal static extern uint MapVirtualKey(uint code, uint mapType);
    [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}


internal static class HidAccess
{
    [DllImport("kernel32.dll", EntryPoint="CreateFileW", CharSet=CharSet.Unicode, SetLastError=true)]
    private static extern Microsoft.Win32.SafeHandles.SafeFileHandle Open(string name, uint desiredAccess, uint share, nint security, uint mode, uint flags, nint template);
    [DllImport("hid.dll")] [return: MarshalAs(UnmanagedType.U1)] private static extern bool HidD_GetPreparsedData(Microsoft.Win32.SafeHandles.SafeFileHandle device, out nint data);
    [DllImport("hid.dll")] [return: MarshalAs(UnmanagedType.U1)] internal static extern bool HidD_FreePreparsedData(nint data);
    internal static Microsoft.Win32.SafeHandles.SafeFileHandle OpenInput(string path) => Open(path, 0x80000000, 3, 0, 3, 0x40000000, 0);
    internal static int ReadAccessError(string path)
    {
        using var device = Open(path, 0x80000000, 3, 0, 3, 0, 0);
        return device.IsInvalid ? Marshal.GetLastWin32Error() : 0;
    }
    internal static nint GetData(string path)
    {
        using var device = Open(path, 0, 3, 0, 3, 0, 0);
        return !device.IsInvalid && HidD_GetPreparsedData(device, out var data) ? data : 0;
    }
}
