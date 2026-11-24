namespace Keycraft;

internal sealed class ChoiceButton : SoftButton
{
    private readonly ContextMenuStrip choices;
    private string[] items = [];
    internal event EventHandler? SelectionChanged;
    internal bool ActivateOnReselect { get; init; }
    internal int SelectedIndex { get; private set; } = -1;

    internal ChoiceButton() : base(false)
    {
        ShowChevron = true;
        Font = Theme.Font(10);
        ForeColor = Theme.Text;
        Cursor = Cursors.Hand;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        AccessibleRole = AccessibleRole.ButtonDropDown;
        choices = new ContextMenuStrip
        {
            ShowImageMargin = false,
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
            Font = Theme.Font(10),
            Renderer = new ToolStripProfessionalRenderer(new MenuColors())
        };
    }

    internal void SetItems(IEnumerable<string> options, int index)
    {
        items = options.ToArray();
        SelectedIndex = index;
        Text = items[index];
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        foreach (ToolStripItem item in choices.Items.Cast<ToolStripItem>().ToArray()) item.Dispose();
        for (var index = 0; index < items.Length; index++)
        {
            var item = new ToolStripMenuItem(items[index]) { Tag = index, ForeColor = index == SelectedIndex ? Theme.Accent : Theme.Text };
            item.Click += (_, _) =>
            {
                int choice = (int)item.Tag!;
                choices.Close();
                SelectChoice(choice);
            };
            choices.Items.Add(item);
        }
        choices.MinimumSize = new Size(Width, 0);
        choices.Show(this, new Point(0, Height + 2));
    }

    internal void SelectChoice(int index)
    {
        if (index == SelectedIndex && !ActivateOnReselect) return;
        SelectedIndex = index;
        Text = items[index];
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) choices.Dispose();
        base.Dispose(disposing);
    }

    private sealed class MenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.Panel;
        public override Color MenuBorder => Theme.Line;
        public override Color MenuItemBorder => Theme.Line;
        public override Color MenuItemSelected => Theme.Field;
    }
}
