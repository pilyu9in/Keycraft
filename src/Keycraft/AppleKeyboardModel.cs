using System.Globalization;
using System.Text.RegularExpressions;

namespace Keycraft;

internal enum KeyboardShape { Ansi, Iso, Jis }
internal enum BoardStyle { CompactLock, FullLock, CompactTouchId, FullTouchId, CompactEject, FullEject }

internal sealed record AppleKeyboardModel(ushort ProductId, string Name, BoardStyle Style, KeyboardShape? Shape = null)
{
    private static readonly Dictionary<ushort, AppleKeyboardModel> Models = new[]
    {
        new AppleKeyboardModel(0x0267, "Magic Keyboard · Lightning", BoardStyle.CompactEject),
        new AppleKeyboardModel(0x026c, "Magic Keyboard · Lightning", BoardStyle.FullEject),
        new AppleKeyboardModel(0x029c, "Magic Keyboard · Lightning", BoardStyle.CompactLock),
        new AppleKeyboardModel(0x029a, "Magic Keyboard · Touch ID", BoardStyle.CompactTouchId),
        new AppleKeyboardModel(0x029f, "Magic Keyboard · Touch ID", BoardStyle.FullTouchId),
        new AppleKeyboardModel(0x0320, "Magic Keyboard · USB-C", BoardStyle.CompactLock),
        new AppleKeyboardModel(0x0321, "Magic Keyboard · Touch ID · USB-C", BoardStyle.CompactTouchId),
        new AppleKeyboardModel(0x0322, "Magic Keyboard · Touch ID · USB-C", BoardStyle.FullTouchId),
    }.Concat(Legacy(0x021d, "Apple Keyboard", BoardStyle.CompactEject))
        .Concat(Legacy(0x0220, "Apple Keyboard", BoardStyle.FullEject))
        .Concat(Legacy(0x024f, "Apple Keyboard", BoardStyle.FullEject))
        .Concat(Legacy(0x022c, "Apple Wireless Keyboard", BoardStyle.CompactEject))
        .Concat(Legacy(0x0239, "Apple Wireless Keyboard", BoardStyle.CompactEject))
        .Concat(Legacy(0x0255, "Apple Wireless Keyboard", BoardStyle.CompactEject))
        .ToDictionary(item => item.ProductId);

    private static readonly Regex DeviceId = new(@"(?:^|[#\\_&])VID(?:_05AC|&000205AC|&0001004C|&05AC)(?=[&#\\_])[^#\\]*?[&_]PID[_&]([0-9A-F]{4})(?=$|[&#\\_])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static IEnumerable<AppleKeyboardModel> Legacy(ushort ansiId, string name, BoardStyle style) =>
        Enum.GetValues<KeyboardShape>().Select(shape => new AppleKeyboardModel((ushort)(ansiId + (int)shape), name, style, shape));

    internal static AppleKeyboardModel? Find(ushort productId) => Models.GetValueOrDefault(productId);

    internal static AppleKeyboardModel? FromPath(string path)
    {
        var match = DeviceId.Match(path);
        return match.Success ? Find(ushort.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)) : null;
    }

    internal static bool IsBluetooth(string path) => path.Contains("BTH", StringComparison.OrdinalIgnoreCase)
        || path.Contains("VID&", StringComparison.OrdinalIgnoreCase);

    internal static bool HasNumberPad(BoardStyle style) => style is BoardStyle.FullLock or BoardStyle.FullTouchId or BoardStyle.FullEject;
}
