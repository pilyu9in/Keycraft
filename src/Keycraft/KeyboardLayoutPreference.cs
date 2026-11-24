namespace Keycraft;

internal static class KeyboardLayoutPreference
{
    private static string SettingsFolder => Path.GetDirectoryName(Profile.PathToProfile)!;

    internal static KeyboardShape? LoadShape() => Read("layout.txt") switch
    {
        "ANSI" => KeyboardShape.Ansi,
        "ISO" => KeyboardShape.Iso,
        "JIS" => KeyboardShape.Jis,
        _ => null
    };

    internal static BoardStyle? LoadModel() => Enum.TryParse<BoardStyle>(Read("model.txt"), out var style) && Enum.IsDefined(style) ? style : null;

    internal static void SaveShape(KeyboardShape? shape) => Write("layout.txt", shape?.ToString().ToUpperInvariant() ?? "Auto");
    internal static void SaveModel(BoardStyle? style) => Write("model.txt", style?.ToString() ?? "Auto");

    private static string? Read(string name)
    {
        var path = Path.Combine(SettingsFolder, name);
        try { return File.Exists(path) ? File.ReadAllText(path).Trim() : null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    private static void Write(string name, string value)
    {
        Directory.CreateDirectory(SettingsFolder);
        File.WriteAllText(Path.Combine(SettingsFolder, name), value);
    }
}
