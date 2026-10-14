// Reading Windows shell shortcuts through the documented IShellLinkW COM interface
// (shobjidl.h). Declared by hand so the project needs no COM NuGet packages.
//
// Note: this intentionally never calls IShellLink::Resolve. Resolve can search
// removable drives or surface UI for a broken shortcut, which would make the
// launcher stall on a stale Start Menu entry. Reading the stored path with
// SLGP_RAWPATH and then verifying it exists on disk is faster and safe.
using System.Runtime.InteropServices;
using System.Text;

namespace NoctaliaWin.Windows.Native;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct WIN32_FIND_DATAW
{
    public uint dwFileAttributes;
    public long ftCreationTime;
    public long ftLastAccessTime;
    public long ftLastWriteTime;
    public uint nFileSizeHigh;
    public uint nFileSizeLow;
    public uint dwReserved0;
    public uint dwReserved1;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
    public string cFileName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
    public string cAlternateFileName;
}

[ComImport]
[Guid("00021401-0000-0000-C000-000000000046")]
[ClassInterface(ClassInterfaceType.None)]
internal class ShellLinkCoclass
{
}

/// <summary>
/// IShellLinkW. The method order below is the vtable order from shobjidl.h; the
/// setters are declared but unused, because omitting a slot would shift everything
/// after it.
/// </summary>
[ComImport]
[Guid("000214F9-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellLinkW
{
    [PreserveSig]
    int GetPath(StringBuilder pszFile, int cchMaxPath, ref WIN32_FIND_DATAW pfd, uint fFlags);

    [PreserveSig]
    int GetIDList(out IntPtr ppidl);

    [PreserveSig]
    int SetIDList(IntPtr pidl);

    [PreserveSig]
    int GetDescription(StringBuilder pszName, int cchMaxName);

    [PreserveSig]
    int SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);

    [PreserveSig]
    int GetWorkingDirectory(StringBuilder pszDir, int cchMaxPath);

    [PreserveSig]
    int SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);

    [PreserveSig]
    int GetArguments(StringBuilder pszArgs, int cchMax);

    [PreserveSig]
    int SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);

    [PreserveSig]
    int GetIconLocation(StringBuilder pszIconFile, int cchMaxPath, out int piIcon);

    [PreserveSig]
    int SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconFile, int iIcon);

    [PreserveSig]
    int SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);

    [PreserveSig]
    int Resolve(IntPtr hwnd, uint fFlags);

    [PreserveSig]
    int SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
}

[ComImport]
[Guid("0000010b-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPersistFile
{
    [PreserveSig]
    int GetClassID(out Guid pClassID);

    [PreserveSig]
    int IsDirty();

    [PreserveSig]
    int Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);

    [PreserveSig]
    int Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);

    [PreserveSig]
    int SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);

    [PreserveSig]
    int GetCurFile(out IntPtr ppszFileName);
}

public sealed record ShortcutTarget(string? Path, string Arguments, string? IconLocation);

/// <summary>Reads the stored target of a .lnk file. STGM_READ = 0.</summary>
public static class ShellLinkReader
{
    private const uint SlgpRawPath = 0x4;
    private const int MaxPath = 260;
    private const uint StgmRead = 0;

    public static ShortcutTarget Read(string shortcutPath)
    {
        object? linkInstance = null;
        try
        {
            linkInstance = new ShellLinkCoclass();
            var persist = (IPersistFile)linkInstance;
            if (persist.Load(shortcutPath, StgmRead) != NativeMethods.S_OK)
                return new(null, "", null);

            var link = (IShellLinkW)linkInstance;

            var pathBuffer = new StringBuilder(MaxPath);
            var find = new WIN32_FIND_DATAW();
            if (link.GetPath(pathBuffer, pathBuffer.Capacity, ref find, SlgpRawPath) != NativeMethods.S_OK)
                return new(null, "", null);

            var argsBuffer = new StringBuilder(MaxPath);
            link.GetArguments(argsBuffer, argsBuffer.Capacity);

            var iconBuffer = new StringBuilder(MaxPath);
            string? icon = link.GetIconLocation(iconBuffer, iconBuffer.Capacity, out _) == NativeMethods.S_OK
                           && iconBuffer.Length > 0
                ? iconBuffer.ToString()
                : null;

            var target = pathBuffer.ToString();
            return new(string.IsNullOrWhiteSpace(target) ? null : target, argsBuffer.ToString(), icon);
        }
        catch (COMException)
        {
            // A corrupt or unreadable shortcut must not abort a discovery sweep.
            return new(null, "", null);
        }
        finally
        {
            if (linkInstance is not null) Marshal.ReleaseComObject(linkInstance);
        }
    }
}
