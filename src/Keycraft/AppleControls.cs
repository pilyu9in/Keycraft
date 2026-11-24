namespace Keycraft;

internal static class AppleControls
{
    internal static readonly SourceKey LockKey = SourceKey.FromHid(0x0c, 0x019e);
    internal static readonly SourceKey FnKey = new("HID-00FF-0003", "Fn / Globe");

    internal static (ushort Page, ushort Usage) Normalize(ushort page, ushort usage) => (page, usage) switch
    {
        (0xff00, 3) => (0x00ff, 3),
        _ => (page, usage)
    };

    internal static bool IsSpecial(ushort page, ushort usage) => (page, usage) is
        (0x00ff or 0xff00, 3) or (0x0c, 0x019e or 0x00b8 or 0x0030 or 0x0040) or (1, 0x81);
}
