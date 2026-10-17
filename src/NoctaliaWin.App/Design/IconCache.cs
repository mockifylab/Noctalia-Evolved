// The launcher's application pictures, read away from the render thread and kept between
// repaints.
//
// Getting an icon means opening a binary from disk, which is far too slow to do while a key is
// being typed: a row asks, and the answer arrives on the UI thread whenever the shell finishes.
// An entry with no icon is remembered as having no icon, so a program that cannot be read is
// not read again on every keystroke.
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NoctaliaWin.Core.Abstractions;

namespace NoctaliaWin.App.Design;

internal sealed class IconCache
{
    private readonly record struct Key(AppEntry Entry, int SizePx);

    private readonly IIconService _icons;
    private readonly Dispatcher _dispatcher;
    private readonly Dictionary<Key, ImageSource?> _loaded = new();
    private readonly Dictionary<Key, List<Action<ImageSource?>>> _waiting = new();

    public IconCache(IIconService icons, Dispatcher dispatcher)
    {
        _icons = icons;
        _dispatcher = dispatcher;
    }

    /// Hands the picture over as soon as it is known, including null for "this application has
    /// no icon". `ready` runs on this cache's dispatcher, so it may touch a visual tree; a row
    /// that has already been repainted away simply gets its source set on a detached element.
    public void Ready(AppEntry entry, int sizePx, Action<ImageSource?> ready)
    {
        var key = new Key(entry, sizePx);
        if (_loaded.TryGetValue(key, out ImageSource? cached))
        {
            ready(cached);
            return;
        }

        if (_waiting.TryGetValue(key, out List<Action<ImageSource?>>? pending))
        {
            pending.Add(ready);
            return;
        }

        _waiting[key] = [ready];
        Task.Run(() => Read(key));
    }

    private void Read(Key key)
    {
        // Decoding happens here rather than on the UI thread, and the image is frozen so the
        // result crosses back over legally.
        ImageSource? image = null;
        if (_icons.IconPng(key.Entry, key.SizePx) is { Length: > 0 } png) image = Decode(png);

        _dispatcher.BeginInvoke(() =>
        {
            _loaded[key] = image;
            if (!_waiting.Remove(key, out List<Action<ImageSource?>>? pending)) return;
            foreach (var ready in pending) ready(image);
        });
    }

    private static ImageSource Decode(byte[] png)
    {
        using var stream = new MemoryStream(png);
        var image = new BitmapImage();
        image.BeginInit();
        // OnLoad reads the whole picture now, which is what lets the stream go.
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
