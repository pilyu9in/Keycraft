using System.Drawing.Drawing2D;

namespace Keycraft;

internal sealed class KeyboardMap : Panel
{
    private readonly Dictionary<string, Keycap> keys = new(StringComparer.Ordinal);
    private readonly ToolTip tips = new() { AutoPopDelay = 8000 };
    private (BoardStyle Style, KeyboardShape Shape)? layout;
    private string? selected;
    private float widthUnits = 15;
    private const string TouchId = "touch-id";
    internal event Action<SourceKey>? KeyChosen;

    internal void SetLayout(BoardStyle style, KeyboardShape shape)
    {
        if (layout == (style, shape)) return;
        layout = (style, shape);
        SuspendLayout();
        tips.RemoveAll();
        foreach (var key in keys.Values) key.Dispose();
        keys.Clear();

        bool full = AppleKeyboardModel.HasNumberPad(style);
        bool legacy = style is BoardStyle.CompactEject or BoardStyle.FullEject;
        bool iso = shape == KeyboardShape.Iso, jis = shape == KeyboardShape.Jis;
        widthUnits = full ? 22.6f : 15;
        BuildFunctionRow(style, legacy);

        Add(1, 0, 1, Scan(0x29, iso ? "§" : "`"), iso ? "§" : "`");
        AddCharacters(1, 1, 0x02, jis ? "1234567890-^" : "1234567890-=");
        if (jis) Add(1, 13, 1, Scan(0x7d, "¥"), "¥");
        Add(1, jis ? 14 : 13, jis ? 1 : 2, Scan(0x0e, "Backspace"), "⌫");

        Add(2, 0, 1.5f, Scan(0x0f, "Tab"), "tab ⇥");
        AddCharacters(2, 1.5f, 0x10, jis ? "QWERTYUIOP@[" : "QWERTYUIOP[]");
        if (iso || jis)
            Add(2, 13.5f, 1.5f, Scan(0x1c, "Enter"), full ? "↵" : "return ↵", height: 2);
        else
            Add(2, 13.5f, 1.5f, Scan(0x2b, "\\"), "\\");

        Add(3, 0, 1.75f, Scan(0x3a, "Caps Lock"), full ? "caps" : "caps lock");
        AddCharacters(3, 1.75f, 0x1e, jis ? "ASDFGHJKL;:" : "ASDFGHJKL;'");
        if (iso || jis)
            Add(3, 12.75f, .75f, Scan(0x2b, jis ? "]" : "#"), jis ? "]" : "#");
        else
            Add(3, 12.75f, 2.25f, Scan(0x1c, "Enter"), "return ↵");

        Add(4, 0, iso ? 1.25f : 2.25f, Scan(0x2a, "Shift"), "shift ⇧");
        if (iso) Add(4, 1.25f, 1, Scan(0x56, "ISO"), "< >");
        AddCharacters(4, 2.25f, 0x2c, "ZXCVBNM,./");
        if (jis) Add(4, 12.25f, 1, Scan(0x73, "_"), "_");
        Add(4, jis ? 13.25f : 12.25f, jis ? 1.75f : 2.75f, Scan(0x36, "Shift"), "shift ⇧");

        BuildBottomRow(full, legacy && full, jis, legacy);
        if (full) BuildNumberPadAndNavigation(legacy);
        ResumeLayout(true);
    }

    private void BuildFunctionRow(BoardStyle style, bool legacy)
    {
        int[] functionKeys = [0x3b, 0x3c, 0x3d, 0x3e, 0x3f, 0x40, 0x41, 0x42, 0x43, 0x44, 0x57, 0x58];
        string?[] media = ["☀−", "☀+", "▣", legacy ? "▦" : "⌕", legacy ? null : "♩", legacy ? null : "☾", "◀◀", "▶Ⅱ", "▶▶", "◖×", "◖−", "◖+"];
        Add(0, 0, 1.35f, Scan(0x01, "Esc"), "esc");
        for (int index = 0; index < functionKeys.Length; ++index)
        {
            string name = $"F{index + 1}";
            Add(0, 1.35f + index * 1.025f, 1.025f, Scan(functionKeys[index], name), name, media[index]);
        }
        var top = style switch
        {
            BoardStyle.CompactTouchId or BoardStyle.FullTouchId => (new SourceKey(TouchId, "Touch ID"), "touch"),
            BoardStyle.CompactEject or BoardStyle.FullEject => (SourceKey.FromHid(0x0c, 0xb8), "⏏"),
            _ => (AppleControls.LockKey, "lock")
        };
        Add(0, 13.65f, 1.35f, top.Item1, top.Item2);
    }

    private void BuildBottomRow(bool full, bool fnInNavigation, bool jis, bool legacy)
    {
        var keys = new List<(SourceKey Source, string Face, float Width)>();
        if (!fnInNavigation) keys.Add((AppleControls.FnKey, legacy ? "fn" : "fn ◉", 1));
        keys.Add((Scan(0x1d, "Control"), full ? "ctrl" : "control", 1.15f));
        keys.Add((Scan(0x38, "Option"), "opt ⌥", 1.15f));
        keys.Add((Scan(0x5b, "Command", true), "cmd ⌘", 1.35f));
        if (jis) keys.Add((Scan(0x71, "英数"), "英数", 1.25f));
        keys.Add((Scan(0x39, "Space"), "⎵", (full ? fnInNavigation ? 7.7f : 6.7f : 4.85f) - (jis ? 2.5f : 0)));
        if (jis) keys.Add((Scan(0x72, "かな"), "かな", 1.25f));
        keys.Add((Scan(0x5c, "Command", true), "cmd ⌘", 1.35f));
        keys.Add((Scan(0x38, "Option", true), "opt ⌥", 1.15f));
        if (full)
            keys.Add((Scan(0x1d, "Control", true), "ctrl", 1.15f));
        else
            keys.Add((Scan(0x4b, "←", true), "←", 1));
        PlaceRow(5, 0, keys);
        if (full) return;
        Add(5, 13, 1, Scan(0x48, "↑", true), "↑", height: .5f);
        Add(5.5f, 13, 1, Scan(0x50, "↓", true), "↓", height: .5f);
        Add(5, 14, 1, Scan(0x4d, "→", true), "→");
    }

    private void BuildNumberPadAndNavigation(bool legacy)
    {
        const float nav = 15.3f, pad = 18.6f;
        for (int index = 0; index < 7; ++index)
        {
            string name = $"F{13 + index}";
            Add(0, (index < 3 ? nav : pad - 3) + index, 1, Scan(0x64 + index, name), name);
        }
        Add(1, nav, 1, legacy ? AppleControls.FnKey : SourceKey.FromHid(0x0c, 0x40), legacy ? "fn" : "☰");
        Add(1, nav + 1, 1, Scan(0x47, "Home", true), "home");
        Add(1, nav + 2, 1, Scan(0x49, "Page Up", true), "pg ↑");
        Add(2, nav, 1, Scan(0x53, "Delete", true), "⌦");
        Add(2, nav + 1, 1, Scan(0x4f, "End", true), "end");
        Add(2, nav + 2, 1, Scan(0x51, "Page Down", true), "pg ↓");
        Add(4, nav + 1, 1, Scan(0x48, "↑", true), "↑");
        Add(5, nav, 1, Scan(0x4b, "←", true), "←");
        Add(5, nav + 1, 1, Scan(0x50, "↓", true), "↓");
        Add(5, nav + 2, 1, Scan(0x4d, "→", true), "→");
        Add(1, pad, 1, Scan(0x45, "Clear", true), "clear");
        Add(1, pad + 1, 1, Scan(0x59, "="), "=");
        Add(1, pad + 2, 1, Scan(0x35, "/", true), "/");
        Add(1, pad + 3, 1, Scan(0x37, "*"), "*");
        AddCharacters(2, pad, 0x47, "789");
        Add(2, pad + 3, 1, Scan(0x4a, "−"), "−");
        AddCharacters(3, pad, 0x4b, "456");
        Add(3, pad + 3, 1, Scan(0x4e, "+"), "+");
        AddCharacters(4, pad, 0x4f, "123");
        Add(4, pad + 3, 1, Scan(0x1c, "Numpad Enter", true), "enter", height: 2);
        Add(5, pad, 2, Scan(0x52, "Num 0"), "0");
        Add(5, pad + 2, 1, Scan(0x53, "Num ."), ".");
    }

    private static SourceKey Scan(int scan, string name, bool extended = false) =>
        new(SourceKey.KeyboardId(new Win.HookKey { Scan = (uint)scan, Flags = extended ? 1u : 0u }), name);

    private void AddCharacters(float row, float column, int scan, string characters)
    {
        foreach (char character in characters)
        {
            string name = character.ToString();
            Add(row, column++, 1, Scan(scan++, name), name);
        }
    }

    private void PlaceRow(float row, float column, IEnumerable<(SourceKey Source, string Face, float Width)> rowKeys)
    {
        foreach (var (source, face, width) in rowKeys)
        {
            Add(row, column, width, source, face);
            column += width;
        }
    }

    private void Add(float row, float column, float width, SourceKey source, string face, string? secondary = null, float height = 1)
    {
        // The function row is shallower and separated from the typing area.
        float y = row == 0 ? 0 : 43 + (row - 1) * 39;
        float keyUnit = 780 / widthUnits;
        var key = new Keycap(face, secondary, new RectangleF(column * keyUnit, y, width * keyUnit - 5, row == 0 ? 36 : height * 39 - 5),
            dense: widthUnits > 15)
        {
            Name = source.Id,
            Selected = selected == source.Id,
            Tag = source
        };
        if (source.Id == TouchId) key.Cursor = Cursors.Default;
        else key.Click += (_, _) => KeyChosen?.Invoke(source);
        keys.Add(source.Id, key);
        Controls.Add(key);
        Position(key);
    }

    protected override bool ScaleChildren => false;

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        foreach (var key in keys.Values) Position(key);
    }

    private void Position(Keycap key)
    {
        var box = key.LayoutBounds;
        float scale = ClientSize.Width / 780f;
        key.Bounds = Rectangle.Round(new RectangleF(box.X * scale, box.Y * scale, box.Width * scale, box.Height * scale));
    }

    internal void Render(Language language, IReadOnlyList<Rule> rules, string? selection)
    {
        selected = selection;
        var byId = rules.ToLookup(rule => rule.Id, StringComparer.Ordinal);
        foreach (var (id, key) in keys)
        {
            var source = (SourceKey)key.Tag!;
            string name = language.Source(id, source.Label);
            var matching = byId[id];
            string detail = !matching.Any() ? name : string.Join("\n", matching.Select(rule =>
                $"{language.Trigger(id, source.Label, rule.Modifiers)} → {language.Action(rule)}"));
            if (id == AppleControls.FnKey.Id) detail += "\n" + language.Text("FnHint");
            if (id == TouchId) detail += "\n" + language.Text("TouchIdHint");
            key.AccessibleName = detail;
            tips.SetToolTip(key, detail);
            key.Selected = id == selected;
            key.Mapped = matching.Any();
            key.Invalidate();
        }
    }

    internal bool HasKey(string id) => keys.ContainsKey(id);

    internal void SetPressed(string id, bool pressed)
    {
        if (!keys.TryGetValue(id, out var key) || key.Pressed == pressed) return;
        key.Pressed = pressed;
        key.Invalidate();
    }

    internal void ClearPressed()
    {
        foreach (var key in keys.Values.Where(key => key.Pressed))
        {
            key.Pressed = false;
            key.Invalidate();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) tips.Dispose();
        base.Dispose(disposing);
    }

    private sealed class Keycap : Button
    {
        private static readonly Color MappedFill = Color.FromArgb(51, 43, 75), MappedStroke = Color.FromArgb(104, 88, 155),
            HoverFill = Color.FromArgb(52, 58, 72);
        private readonly string face;
        private readonly string? secondary;
        private readonly Font legend, small;
        private bool hovered;
        internal bool Selected, Mapped, Pressed;
        internal RectangleF LayoutBounds { get; }

        internal Keycap(string face, string? secondary, RectangleF layoutBounds, bool dense)
        {
            this.face = face;
            this.secondary = secondary;
            LayoutBounds = layoutBounds;
            legend = Theme.Font(face.Length > 2 || secondary != null ? (dense ? 7 : 9) : (dense ? 9.5f : 11), FontStyle.Bold);
            small = Theme.Font(dense ? 6 : 7.5f);
            Cursor = Cursors.Hand;
            BackColor = Theme.Panel;
            TabStop = false;
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Panel);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int Scale(float value) => (int)Math.Round(value * DeviceDpi / 96f);
            using var shape = Theme.Rounded(new Rectangle(1, 1, Width - 3, Height - 3), Scale(7));
            using var fill = new SolidBrush(Pressed ? Theme.Accent : hovered ? HoverFill : Mapped || Selected ? MappedFill : Theme.Field);
            using var border = new Pen(Selected || Focused ? Theme.Accent : Mapped ? MappedStroke : Theme.Line);
            e.Graphics.FillPath(fill, shape);
            e.Graphics.DrawPath(border, shape);
            var ink = Pressed ? Theme.Ink : Mapped || Selected ? Theme.Accent : Theme.Text;
            if (face == "touch")
            {
                using var ring = new Pen(Theme.Subtle, Scale(1.5f));
                int diameter = Scale(14);
                e.Graphics.DrawEllipse(ring, (Width - diameter) / 2, (Height - diameter) / 2, diameter, diameter);
            }
            else if (face == "lock")
            {
                int x = Width / 2, y = Height / 2;
                using var pen = new Pen(ink, Scale(1.5f));
                e.Graphics.DrawArc(pen, x - Scale(4), y - Scale(9), Scale(8), Scale(10), 180, 180);
                using var body = Theme.Rounded(new Rectangle(x - Scale(6), y - Scale(4), Scale(12), Scale(11)), Scale(2));
                e.Graphics.DrawPath(pen, body);
                e.Graphics.DrawLine(pen, x, y, x, y + Scale(3));
            }
            else
            {
                var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
                var label = ClientRectangle;
                if (secondary != null)
                {
                    label.Height = Height / 2 + Scale(2);
                    TextRenderer.DrawText(e.Graphics, secondary, small, new Rectangle(0, Scale(2), Width, label.Height), Pressed ? ink : Theme.Subtle, flags);
                    label.Y = Height / 2 - Scale(1);
                }
                TextRenderer.DrawText(e.Graphics, face, Height < Scale(23) ? small : legend, label, ink, flags);
            }
            if (Mapped)
            {
                using var dot = new SolidBrush(ink);
                e.Graphics.FillEllipse(dot, Width - Scale(10), Scale(5), Scale(4), Scale(4));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { legend.Dispose(); small.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
