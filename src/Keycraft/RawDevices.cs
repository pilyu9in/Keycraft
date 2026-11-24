using System.Runtime.InteropServices;
using System.Text;

namespace Keycraft;

internal sealed class RawDevices : IDisposable
{
    internal sealed record Device(nint Handle, string Path, AppleKeyboardModel? Model, bool Bluetooth, uint Type, Win.HidCaps Caps, nint Data, bool NativeData, Win.ItemCaps[] Buttons, Win.ItemCaps[] Values)
    {
        internal bool IsAppleKeyboard => Model != null;
        internal bool HasStandaloneControls => Data != 0 && (Caps.Page == 0x0c || (Caps.Page == 1 && Caps.Usage == 0x80)
            || ((Caps.Page is 0x00ff or 0xff00) && Buttons.Any(item => AppleControls.IsSpecial(item.Page, item.UsageMin))));
    }
    internal readonly Dictionary<nint, Device> Items = new();
    private readonly HashSet<(ushort Page, ushort Usage)> registered = new();
    public void Refresh(nint target = default)
    {
        uint count = 0, size = (uint)Marshal.SizeOf<Win.DeviceEntry>();
        if (Win.GetRawInputDeviceList(null, ref count, size) == uint.MaxValue) return;
        var entries = new Win.DeviceEntry[count];
        var read = Win.GetRawInputDeviceList(entries, ref count, size);
        if (read == uint.MaxValue) return;
        var connected = entries.Take((int)read).ToArray();
        var handles = connected.Select(item => item.Handle).ToHashSet();
        foreach (var disconnected in Items.Values.Where(item => !handles.Contains(item.Handle)).ToArray())
        {
            Free(disconnected);
            Items.Remove(disconnected.Handle);
        }
        foreach (var item in connected)
        {
            if (Items.ContainsKey(item.Handle)) continue;
            uint chars = 0;
            Win.DeviceBytes(item.Handle, Win.DeviceName, 0, ref chars);
            if (chars == 0 || chars > 4096) continue;
            var name = new StringBuilder((int)chars + 1);
            if (Win.DeviceText(item.Handle, Win.DeviceName, name, ref chars) == uint.MaxValue) continue;
            var path = name.ToString();
            var model = AppleKeyboardModel.FromPath(path);
            var bluetooth = model != null && AppleKeyboardModel.IsBluetooth(path);
            Win.HidCaps caps = default;
            nint preparsed = 0;
            bool nativeData = false;
            if (model != null)
            {
                uint length = 0;
                Win.DeviceBytes(item.Handle, Win.PreparsedData, 0, ref length);
                if (length > 0 && length < 1_000_000)
                {
                    preparsed = Marshal.AllocHGlobal((int)length);
                    if (Win.DeviceBytes(item.Handle, Win.PreparsedData, preparsed, ref length) == uint.MaxValue || Win.HidP_GetCaps(preparsed, out caps) != Win.HidSuccess)
                    {
                        Marshal.FreeHGlobal(preparsed);
                        preparsed = 0;
                    }
                }
            }
            if (model != null && preparsed == 0)
            {
                preparsed = HidAccess.GetData(path);
                nativeData = preparsed != 0;
                if (nativeData) Win.HidP_GetCaps(preparsed, out caps);
            }
            Items[item.Handle] = new Device(item.Handle, path, model, bluetooth, item.Type, caps, preparsed, nativeData,
                preparsed == 0 ? [] : ReadCaps(preparsed, caps.InputButtons, true),
                preparsed == 0 ? [] : ReadCaps(preparsed, caps.InputValues, false));
        }
        if (target != 0)
        {
            Register(target, 1, 6);
            foreach (var d in Items.Values.Where(d => d.IsAppleKeyboard && d.HasStandaloneControls)) Register(target, d.Caps.Page, d.Caps.Usage);
        }
    }

    private void Register(nint target, ushort page, ushort usage)
    {
        if (registered.Contains((page, usage))) return;
        var registration = new Win.Registration { Page=page, Usage=usage, Flags=Win.InputSink | Win.DeviceNotify, Window=target };
        if (Win.RegisterRawInputDevices([registration], 1, (uint)Marshal.SizeOf<Win.Registration>())) registered.Add((page, usage));
    }

    private static Win.ItemCaps[] ReadCaps(nint data, ushort count, bool buttons)
    {
        if (count is 0 or > 1000) return [];
        var caps = new Win.ItemCaps[count];
        var result = buttons ? Win.HidP_GetButtonCaps(0, caps, ref count, data) : Win.HidP_GetValueCaps(0, caps, ref count, data);
        return result == Win.HidSuccess ? caps.Take(count).ToArray() : [];
    }

    internal string Describe()
    {
        var text = new StringBuilder();
        foreach (var d in Items.Values.Where(d => d.IsAppleKeyboard))
        {
            text.AppendLine($"{d.Model!.Name} [{d.Model.ProductId:X4}] {(d.Bluetooth ? "Bluetooth" : "USB")} {(d.Type == Win.Keyboard ? "keyboard" : "HID")} {(d.Data != 0 ? $"page={d.Caps.Page:X4} usage={d.Caps.Usage:X4} bytes={d.Caps.ReportBytes}" : "")}");
        }
        return text.ToString();
    }

    private static void Free(Device device)
    {
        if (device.Data == 0) return;
        if (device.NativeData) HidAccess.HidD_FreePreparsedData(device.Data);
        else Marshal.FreeHGlobal(device.Data);
    }

    private void Clear()
    {
        foreach (var device in Items.Values) Free(device);
        Items.Clear();
    }
    public void Dispose()
    {
        foreach (var (page, usage) in registered)
            Win.RegisterRawInputDevices([new Win.Registration { Page=page, Usage=usage, Flags=Win.Remove }], 1, (uint)Marshal.SizeOf<Win.Registration>());
        registered.Clear();
        Clear();
    }
}
