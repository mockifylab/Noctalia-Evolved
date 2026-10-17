// The offline installer's entry point. It reads the bundle it was shipped inside, shows what
// installing would do, and only then copies anything — no download, no elevation, no silent
// background write, and nothing that replaces a newer build without saying so first.
//
// Usage: NoctaliaEvolved-Setup.exe [payload-folder] [--folder install-folder]
// The wizard still walks every step; `--folder` only pre-picks the destination it would show.
using System.Windows;

namespace NoctaliaWin.Installer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string? payload = null;
        string? folder = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--folder", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    MessageBox.Show("--folder needs a folder after it.",
                        "Noctalia Evolved setup", MessageBoxButton.OK, MessageBoxImage.Error);
                    return 1;
                }

                folder = args[i + 1];
                i++;
                continue;
            }

            // An unrecognised switch is not a payload path, because guessing here would read the
            // wrong folder into a bundle check that has to be exact.
            if (args[i].StartsWith('-') && !System.IO.Path.IsPathRooted(args[i])) continue;
            payload ??= args[i];
        }

        var bundle = InstallerBundle.Read(payload, folder);
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
