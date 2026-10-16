// The offline installer's entry point. It reads the bundle it was shipped inside, shows what
// installing would do, and only then copies anything — no download, no elevation, no silent
// background write, and nothing that replaces a newer build without saying so first.
using System.Windows;

namespace NoctaliaWin.Installer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var bundle = InstallerBundle.Read(args.Length > 0 ? args[0] : null);
        if (!bundle.HasPayload)
        {
            MessageBox.Show(
                bundle.Problem ?? "This installer cannot find the app it is meant to install.",
                "Noctalia Evolved setup",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return 1;
        }

        var application = new Application();
        application.Run(new InstallerWindow(bundle));
        return 0;
    }
}
