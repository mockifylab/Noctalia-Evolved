using System.Drawing;
using System.Drawing.Imaging;
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Core.Apps;
using NoctaliaWin.Windows.Native;

namespace NoctaliaWin.Windows;

/// <summary>
/// Pulls an application's own picture out of the file Windows says holds it, through the same
/// shell entry point Explorer uses, and returns it as PNG bytes.
///
/// Nothing is drawn here and nothing is invented: an entry whose icon the shell cannot read
/// yields null, and the launcher shows that row without a picture. The reference is tried in
/// the order Core sets out, so a shortcut that names no icon falls back to the program it
/// starts before giving up.
/// </summary>
public sealed class ShellIconService : IIconService
{
    public byte[]? IconPng(AppEntry entry, int sizePx)
    {
        if (sizePx <= 0) return null;

        foreach (var reference in AppIconReference.ForEntry(entry))
        {
            if (Extract(reference, sizePx) is { } png) return png;
        }

        return null;
    }

    private static byte[]? Extract(AppIconReference reference, int sizePx)
    {
        IntPtr large = IntPtr.Zero;
        IntPtr small = IntPtr.Zero;

        try
        {
            // The size is packed into both halves of the last argument: low word width, high
            // word height. It applies to the small icon; the large one comes back at 32.
            uint size = (uint)(sizePx | sizePx << 16);
            int result = NativeMethods.SHDefExtractIconW(reference.FilePath, reference.IconIndex,
                0, ref large, ref small, size);

            // S_OK means the file carried the index asked for, S_FALSE means the shell gave the
            // file's default instead. Anything else - a missing binary, an index past the end,
            // a file that is not a container of icons - is a negative HRESULT.
            if (result < 0) return null;

            // Prefer the handle at the size requested, and fall to 32 when the shell only had
            // that one to give.
            IntPtr handle = small != IntPtr.Zero ? small : large;
            return handle == IntPtr.Zero ? null : ToPng(handle);
        }
        // A shortcut's location comes from a file on disk, which can name anything at all: a
        // drive that is gone, a path that is too long, a binary the shell cannot open.
        catch (Exception ex) when (ex is ArgumentException or IOException or OutOfMemoryException
            or System.Runtime.InteropServices.ExternalException)
        {
            return null;
        }
        finally
        {
            // Both handles belong to the caller even when only one was used, and GDI leaks are
            // the kind that outlive the panel that caused them.
            if (large != IntPtr.Zero) NativeMethods.DestroyIcon(large);
            if (small != IntPtr.Zero) NativeMethods.DestroyIcon(small);
        }
    }

    private static byte[]? ToPng(IntPtr iconHandle)
    {
        // FromHandle borrows: the icon stays owned by the shell until DestroyIcon runs, so the
        // bitmap has to be fully converted before the caller gets there.
        using Bitmap bitmap = Icon.FromHandle(iconHandle).ToBitmap();
        if (bitmap.Width == 0 || bitmap.Height == 0) return null;

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }
}
