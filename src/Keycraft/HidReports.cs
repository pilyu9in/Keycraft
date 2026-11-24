using System.Runtime.InteropServices;

namespace Keycraft;

internal sealed class HidReports(RawDevices devices, Action<SourceKey, bool> changed)
{
    private readonly Dictionary<(nint Device, byte Report), HashSet<(ushort Page, ushort Usage)>> states = new();
    private static readonly int HeaderBytes = Marshal.SizeOf<Win.Header>();
    internal void Reset() => states.Clear();

    internal void Read(nint raw)
    {
        uint bytes = 0;
        if (Win.GetRawInputData(raw, Win.Input, 0, ref bytes, (uint)HeaderBytes) != 0 || bytes < HeaderBytes || bytes > 65536) return;
        var ptr = Marshal.AllocHGlobal((int)bytes);
        try
        {
            if (Win.GetRawInputData(raw, Win.Input, ptr, ref bytes, (uint)HeaderBytes) != bytes) return;
            var header = Marshal.PtrToStructure<Win.Header>(ptr);
            if (header.Type != Win.Hid || bytes < HeaderBytes + 8) return;
            if (!devices.Items.TryGetValue(header.Device, out var device) || !device.IsAppleKeyboard || !device.HasStandaloneControls) return;
            var length = Marshal.ReadInt32(ptr, HeaderBytes);
            var count = Marshal.ReadInt32(ptr, HeaderBytes + 4);
            if (length < 1 || count < 1 || (long)length * count > bytes - HeaderBytes - 8) return;
            var report = new byte[length];
            for (var i = 0; i < count; i++)
            {
                Marshal.Copy(ptr + HeaderBytes + 8 + i * length, report, 0, length);
                Parse(device, report);
            }
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }

    private void Parse(RawDevices.Device device, byte[] report)
    {
        var limit = Win.HidP_MaxUsageListLength(0, 0, device.Data);
        if (limit is 0 or > 4096) return;
        var list = new Win.HidUsage[limit];
        if (Win.HidP_GetUsagesEx(0, 0, list, ref limit, device.Data, report, (uint)report.Length) != Win.HidSuccess) return;
        var next = list.Take((int)limit).Where(u => u.Usage != 0)
            .Where(u => device.Caps.Page is 1 or 0x0c || AppleControls.IsSpecial(u.Page, u.Usage))
            .Select(u => AppleControls.Normalize(u.Page, u.Usage)).ToHashSet();
        var id = (device.Handle, report[0]);
        states.TryGetValue(id, out var before);
        before ??= [];
        states[id] = next;
        foreach (var key in before.Except(next))
            if (!states.Values.Any(other => other.Contains(key))) changed(SourceKey.FromHid(key.Page, key.Usage), false);
        foreach (var key in next.Except(before))
            if (!states.Any(entry => entry.Key != id && entry.Value.Contains(key))) changed(SourceKey.FromHid(key.Page, key.Usage), true);
    }
}
