using System.Reflection;

namespace Keycraft;

internal sealed class AboutWindow : Form
{
    private readonly Bitmap artwork;

    internal AboutWindow(Language language, Icon icon)
    {
        SuspendLayout();
        Text = language.Text("AboutTitle");
        Icon = icon;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(366, 160);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ShowInTaskbar = MaximizeBox = MinimizeBox = false;
        BackColor = Theme.Canvas;
        ForeColor = Theme.Text;
        Font = Theme.Font(10);

        artwork = icon.ToBitmap();
        var mark = new PictureBox { Image = artwork, SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(20, 24, 48, 48) };
        var name = new Label { Text = "Keycraft", ForeColor = Theme.Text, Font = Theme.Font(18, FontStyle.Bold), Bounds = new Rectangle(81, 18, 264, 35) };
        var notice = new Label
        {
            Text = typeof(AboutWindow).Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright,
            ForeColor = Theme.Text, Bounds = new Rectangle(82, 57, 262, 22)
        };
        var rights = new Label { Text = language.Text("RightsReserved"), ForeColor = Theme.Subtle, Bounds = new Rectangle(82, 80, 262, 22) };
        var close = new SoftButton(false)
        {
            Text = language.Text("Close"), ForeColor = Theme.Text, Bounds = new Rectangle(223, 111, 123, 33), DialogResult = DialogResult.Cancel
        };
        Controls.AddRange([mark, name, notice, rights, close]);
        AcceptButton = CancelButton = close;
        ResumeLayout(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) artwork.Dispose();
        base.Dispose(disposing);
    }
}
