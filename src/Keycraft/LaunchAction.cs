using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Keycraft;

internal enum LaunchKind { Application, Command }

internal sealed record LaunchAction(LaunchKind Kind, string Value)
{
    internal bool IsValid => Enum.IsDefined(Kind) && !string.IsNullOrWhiteSpace(Value) && Value.Length <= 2048
        && !Value.Contains('\0') && !Value.Contains('\r') && !Value.Contains('\n')
        && (Kind == LaunchKind.Command || Path.IsPathFullyQualified(Value));

    internal string DisplayName => Kind == LaunchKind.Command ? Value : Path.GetFileName(Value);

    internal void Run()
    {
        if (!IsValid) throw new ArgumentException(nameof(Value));
        if (Kind == LaunchKind.Application && !File.Exists(Value)) throw new Win32Exception(2);
        var file = Kind == LaunchKind.Command ? Path.Combine(Environment.SystemDirectory, "cmd.exe") : Value;
        var arguments = Kind == LaunchKind.Command ? $"/d /s /c \"{Value}\"" : "";
        var folder = Kind == LaunchKind.Command ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : Path.GetDirectoryName(file)!;
        int show = Kind == LaunchKind.Command ? 0 : 1;
        using var identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
        {
            using var process = Process.Start(new ProcessStartInfo(file, arguments)
            {
                WorkingDirectory = folder, UseShellExecute = true,
                WindowStyle = show == 0 ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
            });
            return;
        }
        RunFromDesktop(file, arguments, folder, show);
    }

    private static void RunFromDesktop(string file, string arguments, string folder, int show)
    {
        // Start from the interactive desktop shell so actions use the user's normal token.
        var shellType = Type.GetTypeFromProgID("Shell.Application", throwOnError: true)!;
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic windows = shell.Windows();
        object desktopClass = 0, empty = Type.Missing;
        dynamic desktop = windows.FindWindowSW(ref desktopClass, ref empty, 8, out int _, 1);
        if (desktop == null) throw new InvalidOperationException("Explorer desktop is unavailable.");
        desktop.Document.Application.ShellExecute(file, arguments, folder, "open", show);
    }

    internal static bool IsLaunchError(Exception exception) => exception is Win32Exception or COMException
        or InvalidOperationException or ArgumentException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException;
}

internal sealed class LaunchDialog : Form
{
    private readonly TextBox value;
    internal LaunchAction Result => new(kind, value.Text.Trim());
    private readonly LaunchKind kind;

    internal LaunchDialog(Language language, LaunchKind kind, LaunchAction? existing)
    {
        this.kind = kind;
        bool app = kind == LaunchKind.Application;
        Text = language.Text(app ? "LaunchApp" : "RunCommand");
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(554, 152);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MaximizeBox = MinimizeBox = false;
        BackColor = Theme.Canvas;
        ForeColor = Theme.Text;
        Font = Theme.Font(10);
        var label = new Label { Text = language.Text(app ? "AppPath" : "CommandText"), AutoEllipsis = true, Bounds = new Rectangle(20, 15, 514, 23) };
        value = new TextBox { Text = existing?.Kind == kind ? existing.Value : "", MaxLength = 2048, BackColor = Theme.Field,
            ForeColor = Theme.Text, Bounds = new Rectangle(20, 44, app ? 399 : 514, 27), AccessibleName = label.Text,
            PlaceholderText = app ? @"C:\Windows\System32\notepad.exe" : @"start notepad.exe" };
        var browse = new SoftButton(false) { Text = language.Text("Browse"), ForeColor = Theme.Text, Bounds = new Rectangle(427, 40, 107, 34) };
        browse.Click += (_, _) =>
        {
            using var picker = new OpenFileDialog { Filter = $"{language.Text("AppFiles")}|*.exe;*.lnk;*.appref-ms", CheckFileExists = true };
            if (picker.ShowDialog(this) == DialogResult.OK) value.Text = picker.FileName;
        };
        var save = new SoftButton(true) { Text = language.Text("ApplyAction"), Bounds = new Rectangle(393, 98, 141, 36), DialogResult = DialogResult.OK };
        var cancel = new SoftButton(false) { Text = language.Text("Cancel"), ForeColor = Theme.Text,
            Bounds = new Rectangle(253, 98, 132, 36), DialogResult = DialogResult.Cancel };
        void ValidateInput() => save.Enabled = Result.IsValid && (!app || File.Exists(Result.Value));
        value.TextChanged += (_, _) => ValidateInput();
        Controls.AddRange(app ? [label, value, browse, cancel, save] : [label, value, cancel, save]);
        AcceptButton = save;
        CancelButton = cancel;
        ValidateInput();
    }
}
