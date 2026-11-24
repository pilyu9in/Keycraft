namespace Keycraft;
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var session = new Mutex(true, "Local\\Keycraft.57B815D3.Settings", out var first);
        if (!first)
        {
            MessageBox.Show(Language.Load().Text("AlreadyOpen"), "Keycraft");
            return;
        }
        Application.Run(new MainWindow(Profile.PathToProfile));
    }
}
