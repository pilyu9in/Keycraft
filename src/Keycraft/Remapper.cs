using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Keycraft;

internal sealed record SourceKey(string Id, string Label)
{
    internal static string KeyboardId(Win.HookKey key) => key.Scan is 0 or > 0xff ? $"VK-{key.Vk:X2}" : $"SC-{((key.Flags & 1) != 0 ? "E0-" : "")}{key.Scan:X2}";
    internal static SourceKey FromKeyboard(Win.HookKey key)
    {
        if (key.Scan is 0 or > 0xff) return new($"VK-{key.Vk:X2}", KeyNames.Name((int)key.Vk));
        var buffer = new StringBuilder(64);
        var extended = (key.Flags & 1) != 0;
        var name = Win.GetKeyNameText((int)(key.Scan << 16) | (extended ? 1 << 24 : 0), buffer, buffer.Capacity) > 0
            ? buffer.ToString() : KeyNames.Name((int)key.Vk);
        if (key.Vk is 0xa0 or 0xa1 or 0xa2 or 0xa3 or 0xa4 or 0xa5 or 0x5b or 0x5c) name = KeyNames.Name((int)key.Vk);
        return new(KeyboardId(key), name);
    }

    internal static SourceKey FromHid(ushort page, ushort usage)
    {
        var name = (page, usage) switch
        {
            (0x0c, 0x019e) => "Замок (Apple Magic Keyboard)",
            (0x0c, 0x30) => "Power",
            (0x0c, 0xb8) => "Eject",
            (0x0c, 0x40) => "Menu",
            (0x00ff, 3) => "Fn / Globe",
            (0x0c, 0x6f) => "Яркость +",
            (0x0c, 0x70) => "Яркость −",
            (0x0c, 0xb5) => "Следующий трек",
            (0x0c, 0xb6) => "Предыдущий трек",
            (0x0c, 0xcd) => "Пуск / пауза",
            (0x0c, 0xe2) => "Без звука",
            (0x0c, 0xe9) => "Громкость +",
            (0x0c, 0xea) => "Громкость −",
            (0x0c, 0x221) => "Поиск",
            (0x01, 0x81) => "System Power",
            (0x01, 0x82) => "System Sleep",
            _ => $"Спецклавиша {page:X4}:{usage:X4}"
        };
        return new($"HID-{page:X4}-{usage:X4}", name);
    }
}

internal static class KeyNames
{
    internal const int NumpadEnter = 0x10d;
    internal static string Name(int key) => key switch
    {
        0x2e => "Delete", 0x08 => "Backspace", NumpadEnter => "Num Enter", 0x1b => "Esc", 0x20 => "Пробел", 0x0d => "Enter",
        0xa0 => "Левый Shift", 0xa1 => "Правый Shift", 0xa2 => "Левый Ctrl", 0xa3 => "Правый Ctrl",
        0xa4 => "Левый Alt", 0xa5 => "Правый Alt", 0x5b => "Левый Win", 0x5c => "Правый Win",
        0xb3 => "Пуск / пауза", 0xad => "Без звука", 0xae => "Громкость −", 0xaf => "Громкость +",
        >= 0x30 and <= 0x5a => ((char)key).ToString(),
        _ => ((Keys)key).ToString()
    };
    internal static string Chord(int[] keys) => keys.Length == 0 ? "Отключить" : string.Join(" + ", keys.Select(Name));
    internal static int ModifierGroup(int key) => key switch
    {
        0x10 or 0xa0 or 0xa1 => 0x10,
        0x11 or 0xa2 or 0xa3 => 0x11,
        0x12 or 0xa4 or 0xa5 => 0x12,
        0x5b or 0x5c => 0x5b,
        _ => 0
    };
    internal static bool Modifier(int key) => ModifierGroup(key) != 0;
    internal static int[] Groups(IEnumerable<int> keys) => keys.Select(ModifierGroup).Where(key => key != 0).Distinct().Order().ToArray();
    internal static string TriggerId(string id, IEnumerable<int> modifiers) => string.Join("+", modifiers.Select(key => key.ToString("X2")).Append(id));
}

internal sealed record Rule(string Id, string Label, int[] Target, bool Enabled, LaunchAction? Launch = null, int[]? SourceModifiers = null)
{
    internal int[] Modifiers => SourceModifiers ?? [];
    internal string TriggerId => KeyNames.TriggerId(Id, Modifiers);
}
internal sealed record Profile(bool Active, List<Rule> Rules)
{
    internal static string PathToProfile { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Keycraft", "profile.json");
    internal static Profile Default => new(true, []);
    internal static Profile Load(string path)
    {
        if (!File.Exists(path)) return Default;
        var read = JsonSerializer.Deserialize<Profile>(File.ReadAllText(path)) ?? throw new InvalidDataException("Пустой профиль");
        if (read.Rules == null || read.Rules.Any(r => !Valid(r)) || read.Rules.Select(rule => rule.TriggerId).Distinct().Count()!=read.Rules.Count)
            throw new InvalidDataException("Некорректные правила в профиле");
        return read;
    }
    private static bool Valid(Rule r) => r != null && !string.IsNullOrWhiteSpace(r.Label) && r.Target != null
        && r.Target.Length <= 5 && r.Target.All(k => k is > 0 and < 255 or KeyNames.NumpadEnter) && r.Target.Distinct().Count() == r.Target.Length
        && (r.Launch == null || (r.Launch.IsValid && r.Target.Length == 0))
        && r.Modifiers.SequenceEqual(KeyNames.Groups(r.Modifiers))
        && r.Id != null && System.Text.RegularExpressions.Regex.IsMatch(r.Id, "^(SC-(E0-)?[0-9A-F]{2}|VK-[0-9A-F]{2}|HID-[0-9A-F]{4}-[0-9A-F]{4})$");
    internal void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

internal sealed class Remapper : IDisposable
{
    private readonly Win.KeyboardCallback callback;
    private readonly Dictionary<string, Rule> rules = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int[]> held = new(StringComparer.Ordinal);
    private readonly Dictionary<int, int> outputCounts = new();
    private readonly HashSet<string> swallowedUntilUp = new(StringComparer.Ordinal);
    private readonly HashSet<string> physicalDown = new(StringComparer.Ordinal);
    private readonly HashSet<int> physicalModifiers = [];
    internal Action<LaunchAction>? Launch;
    private nint hook;

    private bool active;
    private readonly Control dispatcher;
    internal Action<SourceKey, int, bool>? Capture;
    internal Action<string, bool>? Observe;
    internal bool CaptureTarget;
    internal bool Installed => hook != 0;
    internal Remapper(Control dispatcher)
    {
        this.dispatcher = dispatcher;
        callback = OnKeyboard;
        hook = Win.SetWindowsHookEx(13, callback, Win.GetModuleHandle(null), 0);
    }
    internal void Apply(Profile profile)
    {
        ReleaseAll();
        rules.Clear();
        foreach (var r in profile.Rules.Where(r => r.Enabled)) rules[r.TriggerId] = r;
        active = profile.Active;
    }

    internal void HidChanged(SourceKey key, bool down)
    {
        Observe?.Invoke(key.Id, down);
        if (!down) { Stop(key.Id); return; }
        if (Capture != null)
        {
            if (!CaptureTarget) Capture(key, 0, true);
            return;
        }
        if (active && FindRule(key.Id) is { } rule) Start(key.Id, rule);
    }

    private nint OnKeyboard(int code, nint message, nint data)
    {
        if (code < 0) return Win.CallNextHookEx(hook, code, message, data);
        try
        {
            if (HandleKeyboard(Marshal.PtrToStructure<Win.HookKey>(data))) return 1;
        }
        catch (Exception) { /* Let Windows handle the original event if translation fails. */ }
        return Win.CallNextHookEx(hook, code, message, data);
    }

    internal bool HandleKeyboard(Win.HookKey evt)
    {
        if ((evt.Flags & 0x10) != 0 || evt.Vk is 0 or >= 255) return false;
        var keyId = SourceKey.KeyboardId(evt);
        int key = (int)evt.Vk;
        bool down = (evt.Flags & 0x80) == 0;
        bool repeat = down && !physicalDown.Add(keyId);
        if (!down) physicalDown.Remove(keyId);
        if (KeyNames.Modifier(key))
        {
            if (down) physicalModifiers.Add(key);
            else physicalModifiers.Remove(key);
        }
        if (Observe == null && Capture == null && (!active || rules.Count == 0) && swallowedUntilUp.Count == 0) return false;
        Observe?.Invoke(keyId, down);
        int capturedKey = keyId == "SC-E0-1C" ? KeyNames.NumpadEnter : key;
        if (!down && swallowedUntilUp.Remove(keyId))
        {
            Stop(keyId);
            Capture?.Invoke(SourceKey.FromKeyboard(evt), capturedKey, false);
            return true;
        }
        if (Capture != null)
        {
            if (!down) return false;
            if (repeat) return swallowedUntilUp.Contains(keyId);
            swallowedUntilUp.Add(keyId);
            Capture(SourceKey.FromKeyboard(evt), capturedKey, true);
            return true;
        }
        if (!down) return false;
        if (swallowedUntilUp.Contains(keyId))
        {
            if (held.TryGetValue(keyId, out var mapped) && mapped.Length == 1 && !KeyNames.Modifier(mapped[0]))
                Queue([(mapped[0], false)]);
            return true;
        }
        if (!active || repeat || FindRule(keyId, KeyNames.Modifier(key)) is not { } rule) return false;
        Start(keyId, rule);
        swallowedUntilUp.Add(keyId);
        return true;
    }

    private Rule? FindRule(string id, bool modifierKey = false)
    {
        if (!modifierKey && physicalModifiers.Count > 0)
        {
            var trigger = KeyNames.TriggerId(id, KeyNames.Groups(physicalModifiers));
            if (rules.TryGetValue(trigger, out var chord)) return chord;
        }
        return rules.GetValueOrDefault(id);
    }

    private void Start(string id, Rule rule)
    {
        if (held.ContainsKey(id)) return;
        if (rule.Launch is { } launch)
        {
            held[id] = [];
            if (dispatcher.IsHandleCreated) dispatcher.BeginInvoke(() => Launch?.Invoke(launch));
            return;
        }
        var destination = rule.Target;
        var events = destination.Where(key => !outputCounts.ContainsKey(key)).Select(key => (key, false)).ToArray();
        Queue(events);
        foreach (var key in destination) outputCounts[key] = outputCounts.GetValueOrDefault(key) + 1;
        held[id] = destination;
    }

    private void Stop(string id)
    {
        if (!held.Remove(id, out var mapped)) return;
        var events = new List<(int Key, bool Up)>();
        foreach (var k in mapped.Reverse())
        {
            if (--outputCounts[k] != 0) continue;
            outputCounts.Remove(k);
            events.Add((k, true));
        }
        Queue(events.ToArray());
    }
    internal void ReleaseAll()
    {
        foreach (var id in held.Keys.ToArray()) Stop(id);
    }

    private readonly System.Collections.Generic.Queue<(int Key, bool Up)[]> pending = new();
    private void Queue((int Key, bool Up)[] events)
    {
        if (events.Length == 0) return;
        pending.Enqueue(events);
        if (pending.Count == 1 && dispatcher.IsHandleCreated) dispatcher.BeginInvoke(Flush);
    }
    private void Flush()
    {
        while (pending.TryDequeue(out var events)) Send(events);
    }
    internal static bool Send(IEnumerable<(int Key, bool Up)> events, nuint marker = 0x4b435246)
    {
        var inputs = events.Select(e => new Win.InputEvent
        {
            Type=1,
            Value=new Win.InputValue { Key=new Win.KeyInput { Vk=(ushort)(e.Key == KeyNames.NumpadEnter ? 0x0d : e.Key),
                Flags=(e.Up ? 2u : 0u) | (Extended(e.Key) ? 1u : 0u), Extra=marker } }
        }).ToArray();
        return inputs.Length == 0 || Win.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Win.InputEvent>()) == inputs.Length;
    }
    private static bool Extended(int key) => key is KeyNames.NumpadEnter or 0xa3 or 0xa5 or 0x5b or 0x5c or 0x5d or 0x6f or 0x90 or >= 0x21 and <= 0x28 or 0x2d or 0x2e or >= 0xa6 and <= 0xb7;

    public void Dispose()
    {
        ReleaseAll();
        if (hook != 0) { Win.UnhookWindowsHookEx(hook); hook=0; }
        Flush();
        Capture = null;
    }
}
