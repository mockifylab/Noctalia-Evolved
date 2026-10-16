// The system's media controls, read through Windows.Media.Control. This is the surface a
// keyboard play key and the OS volume overlay drive, so it reports whatever app currently
// owns media — a media player, a browser tab, a podcast app — without any of them
// cooperating with us.
//
// Deliberately not used: the older app-side SystemMediaTransportControls, which only ever
// describes the calling process, and any form of polling, since the manager raises an event
// for every change that matters.
using NoctaliaWin.Core.Abstractions;
using Windows.Foundation;
using Windows.Media.Control;

namespace NoctaliaWin.Windows;

public sealed class MediaService : IMediaService, IDisposable
{
    private readonly object _gate = new();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private MediaSnapshot _snapshot = MediaSnapshot.Empty;

    /// Reads of a session's properties are asynchronous, so a newer session can arrive while
    /// one is in flight. A read only owns the snapshot while this still matches.
    private int _generation;

    public event Action? Changed;

    public MediaSnapshot Snapshot
    {
        get { lock (_gate) return _snapshot; }
    }

    public MediaService() => _ = StartAsync();

    private async Task StartAsync()
    {
        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            lock (_gate)
            {
                _manager = manager;
                // Named handlers, because the manager outlives this service and a lambda
                // attached here would keep it alive for the rest of the session.
                manager.CurrentSessionChanged += ManagerEvent;
                manager.SessionsChanged += ManagerEvent;
            }
            OnSessionChanged();
        }
        catch (Exception)
        {
            // A machine with no session manager has no media to show, and the widget stays
            // hidden rather than claiming a control that reads nothing.
        }
    }

    private void OnSessionChanged()
    {
        GlobalSystemMediaTransportControlsSession? session;
        try
        {
            lock (_gate) session = _manager?.GetCurrentSession();
        }
        catch (Exception)
        {
            session = null;
        }

        lock (_gate)
        {
            DetachSession();
            _session = session;
            _generation++;
            if (session is null) _snapshot = MediaSnapshot.Empty;
        }

        if (session is null)
        {
            Changed?.Invoke();
            return;
        }

        session.PlaybackInfoChanged += SessionEvent;
        session.MediaPropertiesChanged += SessionEvent;
        Refresh();
    }

    /// The session's own events say what changed but not to what, so every one of them takes
    /// a fresh reading of the whole picture.
    private void Refresh() => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        GlobalSystemMediaTransportControlsSession? session;
        int generation;
        lock (_gate)
        {
            session = _session;
            generation = _generation;
        }
        if (session is null) return;

        GlobalSystemMediaTransportControlsSessionPlaybackInfo info;
        string? title;
        string? artist;
        try
        {
            info = session.GetPlaybackInfo();
            var props = await session.TryGetMediaPropertiesAsync();
            title = Clean(props.Title);
            artist = Clean(props.Artist);
        }
        catch (Exception)
        {
            // The player closed between the event and the read. Its own next event, or the
            // manager's, is the accurate answer.
            return;
        }

        lock (_gate)
        {
            if (generation != _generation) return;
            var controls = info.Controls;
            _snapshot = new MediaSnapshot(
                true,
                title,
                artist,
                MapStatus(info.PlaybackStatus),
                controls.IsPlayEnabled || controls.IsPauseEnabled,
                controls.IsNextEnabled,
                controls.IsPreviousEnabled,
                Clean(session.SourceAppUserModelId));
        }

        Changed?.Invoke();
    }

    public void TogglePlayPause() => Send(session => session.TryTogglePlayPauseAsync());

    public void Next() => Send(session => session.TrySkipNextAsync());

    public void Previous() => Send(session => session.TrySkipPreviousAsync());

    /// Hands the request to the player and returns. The outcome arrives back through the
    /// session's own events, which is the same way the OS overlay learns about it.
    private void Send(Func<GlobalSystemMediaTransportControlsSession, IAsyncOperation<bool>> request)
    {
        GlobalSystemMediaTransportControlsSession? session;
        lock (_gate) session = _session;
        if (session is null) return;

        try
        {
            _ = AcceptAsync(request(session));
        }
        catch (Exception)
        {
            // Same as a declined command, caught at the handover instead of the result.
        }
    }

    /// Awaits the player's answer only to keep the exception from going unobserved. The
    /// command itself has already reached it by then.
    private static async Task AcceptAsync(IAsyncOperation<bool> request)
    {
        try
        {
            _ = await request;
        }
        catch (Exception)
        {
            // The player declined the command. The snapshot stays where it was, which is what
            // the bar shows.
        }
    }

    private void DetachManager()
    {
        if (_manager is null) return;
        _manager.CurrentSessionChanged -= ManagerEvent;
        _manager.SessionsChanged -= ManagerEvent;
    }

    private void ManagerEvent(object? sender, object args) => OnSessionChanged();

    private void DetachSession()
    {
        if (_session is null) return;
        _session.PlaybackInfoChanged -= SessionEvent;
        _session.MediaPropertiesChanged -= SessionEvent;
    }

    private void SessionEvent(object? sender, object args) => Refresh();

    internal static MediaPlaybackStatus MapStatus(GlobalSystemMediaTransportControlsSessionPlaybackStatus status) =>
        status switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => MediaPlaybackStatus.Playing,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => MediaPlaybackStatus.Paused,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => MediaPlaybackStatus.Stopped,
            _ => MediaPlaybackStatus.None,
        };

    /// A player that has not filled a field in reports an empty string, which the bar treats
    /// as absent rather than as a name worth showing.
    internal static string? Clean(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    public void Dispose()
    {
        lock (_gate)
        {
            DetachManager();
            DetachSession();
            _session = null;
            _manager = null;
            _snapshot = MediaSnapshot.Empty;
        }
    }
}
