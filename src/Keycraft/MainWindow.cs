using System.Drawing.Drawing2D;

namespace Keycraft;

internal static class Theme
{
    internal static readonly Color Canvas = Color.FromArgb(19, 21, 26), Panel = Color.FromArgb(27, 30, 37),
        Field = Color.FromArgb(35, 39, 48), Line = Color.FromArgb(51, 56, 69), Text = Color.FromArgb(242, 244, 250),
        Subtle = Color.FromArgb(169, 177, 196), Accent = Color.FromArgb(184, 172, 255), Ink = Color.FromArgb(27, 21, 49);
    internal static Font Font(float size, FontStyle style = FontStyle.Regular) => new("Segoe UI", size, style);

    internal static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class Surface : Panel
{
    internal Surface() { DoubleBuffered = true; BackColor = Theme.Panel; }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Canvas);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var outline = Theme.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 12 * DeviceDpi / 96);
        using var fill = new SolidBrush(BackColor);
        using var line = new Pen(Theme.Line);
        e.Graphics.FillPath(fill, outline);
        e.Graphics.DrawPath(line, outline);
    }
}

internal class SoftButton(bool primary) : Button
{
    private bool hovered;
    protected bool ShowChevron { get; init; }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Canvas);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = Theme.Rounded(new Rectangle(1, 1, Width - 3, Height - 3), 8 * DeviceDpi / 96);
        var background = primary && Enabled ? (hovered ? Color.FromArgb(203, 193, 255) : Theme.Accent) :
            hovered && Enabled ? Color.FromArgb(46, 52, 64) : Theme.Field;
        using var fill = new SolidBrush(background);
        using var stroke = new Pen(Focused ? Theme.Accent : Theme.Line);
        e.Graphics.FillPath(fill, shape);
        if (!primary || Focused) e.Graphics.DrawPath(stroke, shape);
        var textBounds = ClientRectangle;
        if (ShowChevron)
        {
            int scale(int n) => n * DeviceDpi / 96;
            textBounds.X += scale(10);
            textBounds.Width -= scale(32);
            int x = Width - scale(17), y = Height / 2;
            using var chevron = new Pen(Theme.Subtle, scale(2)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            e.Graphics.DrawLines(chevron, new Point[] { new(x - scale(4), y - scale(2)), new(x, y + scale(2)), new(x + scale(4), y - scale(2)) });
        }
        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, !Enabled ? Theme.Subtle : primary ? Theme.Ink : ForeColor,
            (ShowChevron ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter) | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}

internal sealed partial class MainWindow : Form
{
    private static readonly int[][] Actions = [[0x2e], [8], [0x1b], [0xd], [0xa2, 0x20], [0xb3], [0xad], []];
    private readonly string profilePath;
    private readonly bool preview;
    private Language language;
    private Profile profile;
    private readonly RawDevices devices = new();
    private readonly HidReports reports;
    private Remapper? engine;
    private readonly HashSet<string> rawHeld = new(StringComparer.Ordinal), usbHeld = new(StringComparer.Ordinal);
    private readonly System.Windows.Forms.Timer captureTimer = new() { Interval = 250 }, refresh = new() { Interval = 1500 };
    private readonly Panel overview, editor;
    private readonly KeyboardMap keyboard;
    private readonly Label keyboardTitle, keyboardHint;
    private readonly ChoiceButton layouts, models;
    private static readonly BoardStyle[] ModelChoices = Enum.GetValues<BoardStyle>();
    private BoardStyle? modelOverride;
    private KeyboardShape? shapeOverride;
    private AppleKeyboardModel? detectedModel;
    private readonly Button pickKey;
    private readonly Label hardware, status, rulesTitle, empty, testLabel, editorTitle, sourceLabel, actionLabel, sourceText, targetText;
    private readonly Button state, add, edit, remove, details, sourceButton, targetButton, save, cancel;
    private readonly ChoiceButton languages, presets;
    private readonly DataGridView grid;
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip trayMenu = new();
    private readonly ToolStripMenuItem trayOpen, trayToggle, trayAbout, trayExit;
    private readonly Button about;
    private readonly HashSet<int> modifiers = [];
    private SourceKey? source;
    private int[]? target;
    private int[] sourceModifiers = [];
    private LaunchAction? launch;
    private string? editing, feedback;
    private bool editorOpen, capturingTarget, updatingPresets, exiting, restoring;
    private DateTime captureEnds;
    private int captureSequence;
    internal TextBox TestBox { get; }
    internal int DetectedAppleCollections => devices.Items.Values.Count(device => device.IsAppleKeyboard);

    internal MainWindow(string profilePath, bool preview = false, Language? initialLanguage = null)
    {
        this.profilePath = profilePath;
        this.preview = preview;
        language = initialLanguage ?? Language.Load();
        try { profile = Profile.Load(profilePath); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            profile = new Profile(false, []);
            feedback = "LoadError";
        }
        reports = new HidReports(devices, (key, down) => HandleHid(key, down, false));
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Keycraft";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        BackColor = Theme.Canvas;
        ForeColor = Theme.Text;
        Font = Theme.Font(10);
        ClientSize = new Size(864, 676);
        AutoScroll = true;
        Icon = MakeIcon();

        var brand = new PictureBox { Image = Icon.ToBitmap(), SizeMode = PictureBoxSizeMode.StretchImage };
        Place(this, brand, 24, 17, 40, 40);
        Label(this, "Keycraft", 22, Theme.Text, 76, 12, 315, 50, true);
        about = Button(this, 411, 19, 158, 37, ShowAbout);
        languages = Place(this, new ChoiceButton(), 579, 19, 124, 37);
        languages.SetItems(Language.All.Select(item => item.Name), language.Index);
        state = Button(this, 713, 19, 127, 37, Toggle);

        var board = Place(this, new Surface(), 24, 71, 816, 333);
        keyboardTitle = Label(board, "", 14, Theme.Text, 17, 10, 265, 28, true);
        hardware = Label(board, "", 9, Theme.Subtle, 17, 37, 265, 18);
        modelOverride = preview ? null : KeyboardLayoutPreference.LoadModel();
        shapeOverride = preview ? null : KeyboardLayoutPreference.LoadShape();
        models = Place(board, new ChoiceButton(), 290, 14, 153, 35);
        layouts = Place(board, new ChoiceButton(), 451, 14, 110, 35);
        pickKey = Button(board, 569, 14, 144, 35, PickPhysicalKey);
        details = Button(board, 721, 14, 80, 35, ShowDevices);
        keyboard = Place(board, new KeyboardMap { BackColor = Theme.Panel }, 20, 65, 780, 235);
        keyboard.KeyChosen += ChooseSource;
        layouts.SelectionChanged += (_, _) => ChangeKeyboardLayout();
        models.SelectionChanged += (_, _) => ChangeKeyboardModel();
        keyboardHint = Label(board, "", 9, Theme.Subtle, 20, 303, 777, 20);

        overview = Place(this, new Panel { BackColor = Theme.Canvas }, 24, 414, 816, 190);
        rulesTitle = Label(overview, "", 14, Theme.Text, 1, 0, 600, 32, true);
        add = Button(overview, 664, 0, 152, 33, () => OpenEditor(null), true);
        var rules = Place(overview, new Surface(), 0, 41, 816, 149);
        grid = Place(rules, CreateGrid(), 12, 4, 792, 102);
        empty = Label(rules, "", 10, Theme.Subtle, 18, 37, 780, 55);
        empty.TextAlign = ContentAlignment.MiddleCenter;
        empty.BringToFront();
        edit = Button(rules, 551, 109, 120, 31, () => { if (Selected is { } rule) OpenEditor(rule); });
        remove = Button(rules, 677, 109, 126, 31, RemoveRule);
        grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && Selected is { } rule) OpenEditor(rule); };
        grid.SelectionChanged += (_, _) => UpdateRuleButtons();

        var testing = Place(this, new Surface(), 24, 612, 816, 37);
        testLabel = Label(testing, "", 9, Theme.Subtle, 13, 2, 111, 33);
        TestBox = Place(testing, new TextBox { BackColor = Theme.Field, ForeColor = Theme.Text,
            Font = Theme.Font(10), BorderStyle = BorderStyle.FixedSingle }, 128, 7, 674, 24);

        editor = Place(this, new Panel { BackColor = Theme.Canvas, Visible = false }, 24, 414, 816, 190);
        editorTitle = Label(editor, "", 14, Theme.Text, 1, 0, 600, 32, true);
        var fields = Place(editor, new Surface(), 0, 41, 816, 107);
        sourceLabel = Label(fields, "", 9, Theme.Subtle, 14, 9, 207, 24);
        sourceText = Label(fields, "", 11, Theme.Text, 14, 36, 222, 33);
        sourceButton = Button(fields, 237, 36, 126, 33, () => BeginCapture(false));
        actionLabel = Label(fields, "", 9, Theme.Subtle, 378, 9, 300, 24);
        presets = Place(fields, new ChoiceButton { ActivateOnReselect = true }, 378, 36, 293, 33);
        presets.SelectionChanged += (_, _) => SetPreset();
        targetButton = Button(fields, 677, 36, 124, 33, () => BeginCapture(true));
        targetText = Label(fields, "", 9, Theme.Subtle, 14, 76, 787, 22);
        cancel = Button(editor, 521, 157, 134, 33, CloseEditor);
        save = Button(editor, 664, 157, 152, 33, SaveRule, true);
        status = Label(this, "", 9, Theme.Subtle, 25, 650, 814, 24);

        trayOpen = new ToolStripMenuItem("", null, (_, _) => Restore());
        trayToggle = new ToolStripMenuItem("", null, (_, _) => Toggle());
        trayExit = new ToolStripMenuItem("", null, (_, _) => { exiting = true; Close(); });
        trayAbout = new ToolStripMenuItem("", null, (_, _) => { Restore(); ShowAbout(); });
        trayMenu.Items.AddRange([trayOpen, trayToggle, trayAbout, trayExit]);
        tray = new NotifyIcon { Text = "Keycraft", Icon = Icon, ContextMenuStrip = trayMenu, Visible = !preview };
        tray.DoubleClick += (_, _) => Restore();
        languages.SelectionChanged += (_, _) => ChangeLanguage();
        captureTimer.Tick += (_, _) => { if (engine?.Capture != null && DateTime.UtcNow >= captureEnds) StopCapture("Timeout"); };
        refresh.Tick += (_, _) => RefreshDevices(false);
        Activated += (_, _) => { if (engine != null) engine.Observe = keyboard.SetPressed; };
        Deactivate += (_, _) =>
        {
            if (engine != null) engine.Observe = null;
            keyboard.ClearPressed();
            if (engine?.Capture != null) StopCapture();
        };
        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized && !preview && !restoring) HideToTray(); };
        Shown += (_, _) => { RenderRules(); if (!preview) Install(); else RefreshDevices(false); };
        DpiChanged += (_, _) => Post(RenderRules);
        FormClosed += (_, _) =>
        {
            captureTimer.Dispose(); refresh.Dispose(); tray.Dispose(); trayMenu.Dispose();
            DisposeExtendedInput(); engine?.Dispose(); devices.Dispose(); brand.Image?.Dispose();
        };
        RenderLanguage();
        ResumeLayout(false);
    }

    private static T Place<T>(Control parent, T control, int x, int y, int width, int height) where T : Control
    {
        control.SetBounds(x, y, width, height);
        parent.Controls.Add(control);
        return control;
    }

    private static Label Label(Control parent, string text, float size, Color color, int x, int y, int width, int height, bool bold = false) =>
        Place(parent, new Label { Text = text, ForeColor = color, BackColor = parent.BackColor, AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft, Font = Theme.Font(size, bold ? FontStyle.Bold : FontStyle.Regular) }, x, y, width, height);

    private static Button Button(Control parent, int x, int y, int width, int height, Action clicked, bool primary = false)
    {
        var button = new SoftButton(primary) { Font = Theme.Font(9.5f, FontStyle.Bold), Cursor = Cursors.Hand,
            ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat, UseVisualStyleBackColor = false };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) => clicked();
        return Place(parent, button, x, y, width, height);
    }


    private static DataGridView CreateGrid()
    {
        var view = new DataGridView
        {
            BackgroundColor = Theme.Panel, BorderStyle = BorderStyle.None, RowHeadersVisible = false, ReadOnly = true,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, AllowUserToResizeColumns = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None, GridColor = Theme.Line, EnableHeadersVisualStyles = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing, ColumnHeadersHeight = 31,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ScrollBars = ScrollBars.Vertical,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Theme.Panel, ForeColor = Theme.Subtle,
                Font = Theme.Font(9), Padding = new Padding(8, 0, 0, 0), SelectionBackColor = Theme.Panel, SelectionForeColor = Theme.Subtle },
            DefaultCellStyle = new DataGridViewCellStyle { BackColor = Theme.Panel, ForeColor = Theme.Text,
                SelectionBackColor = Theme.Field, SelectionForeColor = Theme.Text, Font = Theme.Font(10), Padding = new Padding(8, 0, 0, 0) }
        };
        view.RowTemplate.Height = 39;
        view.Columns.Add(new DataGridViewTextBoxColumn { Name = "source", FillWeight = 45, SortMode = DataGridViewColumnSortMode.NotSortable });
        view.Columns.Add(new DataGridViewTextBoxColumn { Name = "direction", HeaderText = " ", FillWeight = 9, SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, ForeColor = Theme.Accent, Padding = Padding.Empty } });
        view.Columns.Add(new DataGridViewTextBoxColumn { Name = "target", FillWeight = 46, SortMode = DataGridViewColumnSortMode.NotSortable });
        return view;
    }

    private static Icon MakeIcon()
    {
        using var image = new Bitmap(48, 48);
        using var g = Graphics.FromImage(image);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = Theme.Rounded(new Rectangle(1, 1, 46, 46), 12);
        using var fill = new SolidBrush(Theme.Accent);
        using var pen = new Pen(Theme.Ink, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.FillPath(fill, shape);
        using var keyboard = Theme.Rounded(new Rectangle(11, 15, 26, 18), 4);
        g.DrawPath(pen, keyboard);
        for (int i = 0; i < 4; ++i) g.DrawLine(pen, 16 + i * 5, 21, 17 + i * 5, 21);
        g.DrawLine(pen, 19, 27, 29, 27);
        var native = image.GetHicon();
        using var shared = Icon.FromHandle(native);
        var icon = (Icon)shared.Clone();
        DestroyIcon(native);
        return icon;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint icon);

    private string L(string key) => language.Text(key);
    private void ChangeLanguage()
    {
        var selected = Language.All[languages.SelectedIndex];
        if (selected == language) return;
        language = selected;
        if (!preview)
        {
            try { language.Save(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { feedback = "SaveError"; }
        }
        RenderLanguage();
    }

    private void RenderLanguage()
    {
        languages.AccessibleName = L("Language");
        layouts.AccessibleName = L("KeyboardLayout");
        models.AccessibleName = L("KeyboardModel");
        models.SetItems(ModelChoices.Select(style => L(style.ToString())).Prepend(L("AutoModel")),
            modelOverride is { } style ? Array.IndexOf(ModelChoices, style) + 1 : 0);
        RenderLayouts();
        keyboardTitle.Text = keyboard.AccessibleName = L("KeyboardTitle");
        keyboardHint.Text = "•  " + L("KeyboardHint");
        details.Text = L("DetailsShort");
        details.AccessibleName = L("Details");
        rulesTitle.Text = L("Rules");
        add.Text = L("Add");
        edit.Text = L("Edit");
        remove.Text = L("Remove");
        empty.Text = L("Empty");
        testLabel.Text = L("Test");
        TestBox.AccessibleName = L("Test");
        TestBox.PlaceholderText = L("TestHint");
        sourceLabel.Text = L("Source");
        actionLabel.Text = L("Action");
        presets.AccessibleName = L("Action");
        cancel.Text = L("Cancel");
        save.Text = L("Save");
        about.Text = trayAbout.Text = L("About");
        trayOpen.Text = L("Open");
        trayToggle.Text = L("Toggle");
        trayExit.Text = L("Exit");
        grid.Columns[0].HeaderText = L("Source");
        grid.Columns[2].HeaderText = L("Action");
        RenderRules();
        RenderHardware();
        RenderEditor();
        RenderStatus();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var dark = 1;
        Win.DwmSetWindowAttribute(Handle, 20, ref dark, 4);
        // Windows 11 applies the rounded frame to fixed windows when requested explicitly.
        var round = 2;
        Win.DwmSetWindowAttribute(Handle, 33, ref round, 4);
    }

    partial void StartExtendedInput();
    partial void RefreshExtendedInput();
    partial void DisposeExtendedInput();
    partial void GetExtendedInputStatus(ref string text);

    private void Install()
    {
        engine = new Remapper(this) { Launch = StartLaunch };
        if (ContainsFocus) engine.Observe = keyboard.SetPressed;
        engine.Apply(profile);
        StartExtendedInput();
        RefreshDevices(true);
        captureTimer.Start();
        refresh.Start();
    }

    private void Post(Action action)
    {
        if (!IsHandleCreated || IsDisposed) return;
        try { BeginInvoke(action); }
        catch (InvalidOperationException) { }
    }

    private void HandleHid(SourceKey key, bool down, bool overUsb)
    {
        var own = overUsb ? usbHeld : rawHeld;
        var other = overUsb ? rawHeld : usbHeld;
        if ((down ? own.Add(key.Id) : own.Remove(key.Id)) && !other.Contains(key.Id))
            engine?.HidChanged(key, down);
    }

    private void RefreshDevices(bool reset)
    {
        var prior = devices.Items.Values.Where(device => device.IsAppleKeyboard).Select(device => device.Handle).Order().ToArray();
        devices.Refresh(Handle);
        var current = devices.Items.Values.Where(device => device.IsAppleKeyboard).Select(device => device.Handle).Order();
        if (reset || !prior.SequenceEqual(current))
        {
            reports.Reset();
            foreach (var id in rawHeld.ToArray()) HandleHid(new SourceKey(id, id), false, false);
            RefreshExtendedInput();
        }
        RenderHardware();
    }

    private void RenderHardware()
    {
        var device = devices.Items.Values.Where(device => device.IsAppleKeyboard && device.Type == Win.Keyboard)
            .OrderBy(device => device.Bluetooth).ThenBy(device => device.Model!.ProductId).FirstOrDefault();
        var modelChanged = detectedModel != device?.Model;
        detectedModel = device?.Model;
        hardware.Text = device == null ? L("NoDevice") : language.Format("Connected", detectedModel!.Name, device.Bluetooth ? "Bluetooth" : "USB");
        hardware.ForeColor = device == null ? Theme.Subtle : Theme.Text;
        if (modelChanged) RenderLayouts();
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == Win.MessageInput) reports?.Read(message.LParam);
        else if (message.Msg == Win.MessageDeviceChange) Post(() => RefreshDevices(true));
        base.WndProc(ref message);
    }

    private void ShowAbout()
    {
        using var window = new AboutWindow(language, Icon ?? SystemIcons.Application);
        window.ShowDialog(this);
    }

    private void ShowDevices()
    {
        RefreshDevices(false);
        var info = devices.Describe();
        MessageBox.Show(this, info.Length == 0 ? L("NoDevice") : info, L("Details"), MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!preview && !exiting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
        }
        base.OnFormClosing(e);
    }

    private void HideToTray()
    {
        if (engine?.Capture != null) StopCapture();
        if (engine != null) engine.Observe = null;
        keyboard.ClearPressed();
        Hide();
    }

    private void Restore()
    {
        restoring = true;
        try
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }
        finally { restoring = false; }
    }

    private void RenderLayouts()
    {
        var shape = shapeOverride ?? detectedModel?.Shape ?? KeyboardShape.Ansi;
        layouts.SetItems([$"{L("AutoShort")} · {shape.ToString().ToUpperInvariant()}", "ANSI", "ISO", "JIS"],
            shapeOverride is { } selectedShape ? (int)selectedShape + 1 : 0);
        keyboard.SetLayout(modelOverride ?? detectedModel?.Style ?? BoardStyle.CompactLock, shape);
        RenderKeyboard();
    }

    private void ChangeKeyboardLayout()
    {
        shapeOverride = layouts.SelectedIndex == 0 ? null : (KeyboardShape)(layouts.SelectedIndex - 1);
        RenderLayouts();
        if (preview) return;
        try { KeyboardLayoutPreference.SaveShape(shapeOverride); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { SetFeedback("SaveError"); }
    }

    private void ChangeKeyboardModel()
    {
        modelOverride = models.SelectedIndex == 0 ? null : ModelChoices[models.SelectedIndex - 1];
        RenderLayouts();
        if (preview) return;
        try { KeyboardLayoutPreference.SaveModel(modelOverride); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { SetFeedback("SaveError"); }
    }

    private void PickPhysicalKey()
    {
        if (!editorOpen) OpenEditor(null);
        BeginCapture(false);
    }

    private void ChooseSource(SourceKey key) => ChooseSource(key, []);

    private void ChooseSource(SourceKey key, int[] triggerModifiers)
    {
        var triggerId = KeyNames.TriggerId(key.Id, triggerModifiers);
        OpenEditor(profile.Rules.FirstOrDefault(rule => rule.TriggerId == triggerId), key, triggerModifiers);
    }

    private void StartLaunch(LaunchAction action)
    {
        _ = Task.Run(() =>
        {
            try { action.Run(); }
            catch (Exception ex) when (LaunchAction.IsLaunchError(ex))
            {
                Post(() =>
                {
                    SetFeedback("LaunchError");
                    if (!Visible) tray.ShowBalloonTip(3500, "Keycraft", L("LaunchError"), ToolTipIcon.Error);
                });
            }
        });
    }

    private void BeginCapture(bool destination)
    {
        if (engine?.Installed != true) { SetFeedback("CaptureError"); return; }
        if (engine.Capture != null) { StopCapture(); return; }
        feedback = null;
        capturingTarget = destination;
        captureEnds = DateTime.UtcNow.AddSeconds(12);
        modifiers.Clear();
        engine.CaptureTarget = destination;
        var generation = ++captureSequence;
        engine.Capture = (key, vk, down) =>
        {
            if (!down)
            {
                if (modifiers.Remove(vk) && modifiers.Count == 0)
                    FinishCapture(generation, key, destination ? [vk] : null, []);
                return;
            }
            if (KeyNames.Modifier(vk)) { modifiers.Add(vk); return; }
            var chord = destination ? modifiers.Order().Take(4).Append(vk).ToArray() : null;
            FinishCapture(generation, key, chord, destination ? [] : KeyNames.Groups(modifiers));
        };
        RenderEditor(false);
        RenderStatus();
    }

    private void FinishCapture(int generation, SourceKey key, int[]? chord, int[] triggerModifiers) => Post(() =>
    {
        if (generation != captureSequence || engine?.Capture == null) return;
        if (chord == null)
        {
            if (key.Id == "SC-56" && !keyboard.HasKey(key.Id)) layouts.SelectChoice((int)KeyboardShape.Iso + 1);
            ChooseSource(key, triggerModifiers);
            return;
        }
        target = chord;
        launch = null;
        StopCapture();
    });

    private void StopCapture(string? message = null)
    {
        if (engine != null) engine.Capture = null;
        ++captureSequence;
        modifiers.Clear();
        RenderEditor();
        SetFeedback(message);
    }

    private void SetPreset()
    {
        if (updatingPresets) return;
        var selected = presets.SelectedIndex - 1;
        if (selected == Actions.Length) { BeginCapture(true); return; }
        if (selected > Actions.Length)
        {
            var kind = selected == Actions.Length + 1 ? LaunchKind.Application : LaunchKind.Command;
            using var dialog = new LaunchDialog(language, kind, launch);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                launch = dialog.Result;
                target = [];
            }
            RenderEditor();
            return;
        }
        launch = null;
        target = selected >= 0 ? Actions[selected] : null;
        RenderEditor(false);
        SetFeedback(null);
    }

    private void Toggle() => ApplyProfile(profile with { Active = !profile.Active }, null);

    private bool ApplyProfile(Profile next, string? message)
    {
        if (engine?.Capture != null) StopCapture();
        try { next.Save(profilePath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { SetFeedback("SaveError"); return false; }
        profile = next;
        engine?.Apply(profile);
        RenderRules();
        SetFeedback(message);
        return true;
    }

    private Rule? Selected => grid.SelectedRows.Count == 0 ? null : grid.SelectedRows[0].Tag as Rule;

    private void OpenEditor(Rule? rule, SourceKey? chosen = null, int[]? triggerModifiers = null)
    {
        editing = rule?.TriggerId;
        target = rule?.Target;
        launch = rule?.Launch;
        sourceModifiers = rule?.Modifiers ?? triggerModifiers ?? [];
        source = rule == null ? chosen : new SourceKey(rule.Id, rule.Label);
        editorOpen = true;
        overview.Visible = false;
        editor.Visible = true;
        StopCapture();
        if (source == null) sourceButton.Focus();
        else presets.Focus();
    }

    private void CloseEditor()
    {
        editorOpen = false;
        StopCapture();
        editor.Visible = false;
        overview.Visible = true;
        add.Focus();
    }

    private void SaveRule()
    {
        if (source == null || target == null) return;
        if (source.Id.StartsWith("HID-", StringComparison.Ordinal) && source.Id != AppleControls.LockKey.Id && target.Length == 0 && launch == null)
        {
            SetFeedback("HidDisable");
            return;
        }
        var rule = new Rule(source.Id, source.Label, target, true, launch, sourceModifiers.Length == 0 ? null : sourceModifiers);
        var updated = profile.Rules.ToList();
        var index = updated.FindIndex(item => item.TriggerId == rule.TriggerId);
        if (index >= 0) updated[index] = rule;
        else updated.Add(rule);
        if (ApplyProfile(profile with { Rules = updated }, "Saved"))
        {
            CloseEditor();
            var row = grid.Rows.Cast<DataGridViewRow>().First(item => ((Rule)item.Tag!).TriggerId == rule.TriggerId);
            grid.CurrentCell = row.Cells[0];
            row.Selected = true;
            SetFeedback("Saved");
        }
    }

    private void RemoveRule()
    {
        if (Selected is not Rule rule) return;
        ApplyProfile(profile with { Rules = profile.Rules.Where(item => item.TriggerId != rule.TriggerId).ToList() }, "Removed");
    }

    private void RenderRules()
    {
        var previous = Selected?.TriggerId;
        grid.Rows.Clear();
        grid.ColumnHeadersHeight = 31 * DeviceDpi / 96;
        grid.RowTemplate.Height = 34 * DeviceDpi / 96;
        foreach (var rule in profile.Rules)
        {
            var row = grid.Rows[grid.Rows.Add(language.Trigger(rule.Id, rule.Label, rule.Modifiers), "→", language.Action(rule))];
            row.Tag = rule;
        }
        empty.Visible = profile.Rules.Count == 0;
        grid.ClearSelection();
        var selected = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(row => ((Rule)row.Tag!).TriggerId == previous) ?? grid.Rows.Cast<DataGridViewRow>().FirstOrDefault();
        if (selected != null) { grid.CurrentCell = selected.Cells[0]; selected.Selected = true; }
        UpdateRuleButtons();
        RenderKeyboard();
    }

    private void UpdateRuleButtons() => edit.Enabled = remove.Enabled = Selected != null;

    private void RenderKeyboard() => keyboard.Render(language, profile.Rules, editorOpen ? source?.Id : null);

    private void RenderEditor(bool updatePresets = true)
    {
        editorTitle.Text = L(editing == null ? "New" : "EditTitle");
        bool capture = engine?.Capture != null;
        pickKey.Text = L(capture && !capturingTarget ? "Cancel" : "PickKey");
        pickKey.Enabled = !capture || !capturingTarget;
        sourceText.Text = source == null ? L("NoSource") : language.Trigger(source.Id, source.Label, sourceModifiers);
        sourceButton.Text = L(capture && !capturingTarget ? "Cancel" : "Capture");
        targetButton.Text = L(capture && capturingTarget ? "Cancel" : "Record");
        sourceButton.Enabled = !capture || !capturingTarget;
        targetButton.Enabled = !capture || capturingTarget;
        presets.Enabled = !capture;
        save.Enabled = source != null && target != null && !capture;
        int index = target == null || launch != null ? -1 : Array.FindIndex(Actions, action => action.SequenceEqual(target));
        var chord = launch?.Value ?? (target != null && index < 0 ? language.Chord(target) : "");
        targetText.Text = source?.Id == AppleControls.FnKey.Id ? (chord.Length == 0 ? "" : chord + "  ·  ") + L("FnHint") : chord;
        if (!updatePresets) return;
        RenderKeyboard();
        updatingPresets = true;
        presets.SetItems(Actions.Select(language.Chord).Prepend(L("ChooseAction")).Concat([L("Custom"), L("LaunchApp"), L("RunCommand")]),
            launch is { } action ? Actions.Length + (action.Kind == LaunchKind.Application ? 2 : 3) :
                index >= 0 ? index + 1 : target == null ? 0 : Actions.Length + 1);
        updatingPresets = false;
    }

    private void SetFeedback(string? message) { feedback = message; RenderStatus(); }
    private void RenderStatus()
    {
        state.Text = (profile.Active ? "●  " : "Ⅱ  ") + L(profile.Active ? "Enabled" : "Paused");
        state.ForeColor = profile.Active ? Theme.Accent : Theme.Subtle;
        state.AccessibleName = L("Toggle") + " · " + L(profile.Active ? "Enabled" : "Paused");
        tray.Text = "Keycraft · " + L(profile.Active ? "Enabled" : "Paused");
        var inputStatus = "";
        GetExtendedInputStatus(ref inputStatus);
        status.Text = engine?.Capture != null ? L(capturingTarget ? "CaptureTarget" : "CaptureSource") :
            feedback != null ? L(feedback) : !profile.Active || editorOpen ? "" : inputStatus;
    }
}
